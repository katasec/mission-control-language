using ForgeMission.Application.Transport;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Application;

/// <summary>A chat's complete ordered history, and the sequence its live stream resumes from. There
/// is no cursor here and no "more remains" flag: the caller receives all of it or a typed failure.</summary>
internal sealed record MissionChatHistory(IReadOnlyList<ConversationEvent> Events, long ThroughSequence);

/// <summary>A typed history outcome, not a bug: a page read that failed, or a page that contradicted
/// itself. It never leaves Application as anything but its own failure code.</summary>
internal sealed class MissionChatHistoryException(MissionChatFailureCode code, string message)
    : Exception(message)
{
    public MissionChatFailureCode Code { get; } = code;
}

/// <summary>
/// The one owner of opening a Mission Chat's history and its live tail (Phase 48). Nothing else
/// reads a snapshot, reads a page, or starts a reader, so the gapless order below exists in exactly
/// one place:
/// <list type="number">
/// <item>dispose the previous tail;</item>
/// <item>read the snapshot once and fix its LastSequence as the upper bound;</item>
/// <item>page to that fixed bound, appending each page and advancing the cursor to the sequence the
/// page actually returned;</item>
/// <item>finish only when the cursor equals the bound;</item>
/// <item>start the tail at that same sequence.</item>
/// </list>
/// The bound is fixed before the first page, so a live event arriving mid-assembly cannot move the
/// target; every iteration must advance the cursor, so the loop can neither spin nor stop early; and
/// the tail resumes at the bound, so the next durable event is exactly the reader's expected next
/// sequence. It passes no event hook to the reader, so this path structurally has no route to a
/// capability dispatcher.
/// </summary>
internal sealed class MissionChatStream(
    string sessionId,
    MissionConversationService conversations,
    ConversationHostClient host,
    Action<ApplicationEvent> publish,
    CancellationToken applicationStopping) : IAsyncDisposable
{
    private ConversationTailReader? _tail;

    internal async Task<MissionChatHistory> OpenAsync(Guid conversationId, CancellationToken ct)
    {
        await StopTailAsync();

        var snapshot = await ReadAsync(() => conversations.ReadSnapshotAsync(conversationId, ct));
        var through = snapshot.LastSequence;
        var seed = new List<ConversationEvent>();
        var cursor = 0L;
        while (cursor < through)
        {
            var page = await ReadAsync(() => conversations.ReadEventsAsync(conversationId, cursor, through, ct));
            seed.AddRange(page.Events);
            cursor = NextCursor(page, cursor, through);
        }

        var reader = new ConversationTailReader(sessionId, host, publish, applicationStopping);
        try
        {
            await reader.StartAsync(conversationId, through, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await reader.DisposeAsync();
            throw new MissionChatHistoryException(MissionChatFailureCode.StreamLost, exception.Message);
        }

        _tail = reader;
        return new MissionChatHistory(seed, through);
    }

    /// <summary>The page's own returned sequence is the only legal next cursor. A page that claims
    /// more while standing still, or claims completeness short of the fixed bound, is a protocol
    /// fault: it fails here rather than looping, stopping early, or presenting a partial history as
    /// the whole of it.</summary>
    private static long NextCursor(MissionConversationEventPage page, long cursor, long through)
    {
        var next = page.ReturnedThroughSequence;
        if (next > through || (page.HasMore ? next <= cursor : next < through))
            throw new MissionChatHistoryException(MissionChatFailureCode.HistoryProtocol,
                "The conversation history page did not advance to its requested bound.");
        return next;
    }

    private static async Task<T> ReadAsync<T>(Func<Task<T>> read)
    {
        try
        {
            return await read();
        }
        catch (Exception exception) when (exception is ConversationHostProjectException or HttpRequestException
            or ConversationHostProtocolException)
        {
            throw new MissionChatHistoryException(MissionChatFailureCode.HistoryUnavailable, exception.Message);
        }
    }

    private async Task StopTailAsync()
    {
        if (_tail is null)
            return;

        var tail = _tail;
        _tail = null;
        await tail.DisposeAsync();
    }

    public async ValueTask DisposeAsync() => await StopTailAsync();
}

/// <summary>
/// One Mission Chat stream per live application session, with the same single-entry-point shape the
/// session's other slots use: admission and use are serialized against disposal, so a replaced
/// session can never leave a tail running after its own disposal returned.
/// </summary>
internal sealed class MissionChatSlot : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _disposeGate = new();
    private MissionChatStream? _stream;
    private Task? _dispose;
    private bool _closed;

    internal async Task<MissionChatHistory> OpenAsync(Func<MissionChatStream> factory, Guid conversationId, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_closed)
                throw new InvalidOperationException("This application session has been replaced.");

            _stream ??= factory();
            return await _stream.OpenAsync(conversationId, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposeGate)
        {
            _dispose ??= DisposeCoreAsync();
            return new ValueTask(_dispose);
        }
    }

    private async Task DisposeCoreAsync()
    {
        MissionChatStream? toDispose;
        await _gate.WaitAsync();
        try
        {
            if (_closed)
                return;

            _closed = true;
            toDispose = _stream;
        }
        finally
        {
            _gate.Release();
        }

        if (toDispose is not null)
            await toDispose.DisposeAsync();
    }
}
