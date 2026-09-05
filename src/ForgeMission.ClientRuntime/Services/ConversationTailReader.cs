using ForgeMission.ClientRuntime.Transport;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ClientRuntime.Services;

// The durable replay/tail half of a Client Runtime conversation session, factored out of
// ConversationRuntimeSession when Project Mission Control became its second consumer (43.20 task 2).
// Owns exactly one thing: following a conversation's ordered ConversationEvent stream and relaying
// each event onward once, reconnecting from the last delivered sequence.
//
// The optional onEventAsync hook is the ONLY behavioural difference between its two consumers: the
// Janus session passes its local tool hand-off, and a Project-control session passes null — so a
// control session structurally has no path to a capability dispatcher at all, rather than a rule
// saying it must not use one.
internal sealed class ConversationTailReader : IAsyncDisposable
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromMilliseconds(250);

    private readonly string _sessionId;
    private readonly ConversationHostClient _hostClient;
    private readonly Action<ClientRuntimeEvent> _publish;
    private readonly Func<ConversationEvent, CancellationToken, Task>? _onEventAsync;
    private readonly CancellationTokenSource _lifetimeCts;

    // Tail-loop-only state: touched exclusively by the single TailAsync loop, never concurrently
    // with a send, so no additional locking is needed. Sequence is the durable dedupe key; an
    // unbounded EventId set would retain every historical trace for the whole session lifetime.
    private long _lastSequence;

    private Task? _tailTask;
    private TaskCompletionSource _connected = NewConnectedSignal();

    public ConversationTailReader(
        string sessionId,
        ConversationHostClient hostClient,
        Action<ClientRuntimeEvent> publish,
        CancellationToken applicationStopping,
        Func<ConversationEvent, CancellationToken, Task>? onEventAsync = null)
    {
        _sessionId = sessionId;
        _hostClient = hostClient;
        _publish = publish;
        _onEventAsync = onEventAsync;
        _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(applicationStopping);
    }

    /// <summary>Starts following <paramref name="conversationId"/> from <paramref name="afterSequence"/>, so a reopened
    /// conversation replays its whole durable history before live events arrive. Idempotent: a
    /// second call while a tail is already running is a no-op.</summary>
    public void Start(Guid conversationId, long afterSequence = 0)
    {
        if (_tailTask is not null)
            return;

        _lastSequence = afterSequence;
        _tailTask = Task.Run(() => TailAsync(conversationId, _lifetimeCts.Token));
    }

    /// <summary>Starts the SSE subscription and waits until Host accepted it. Read owners use
    /// this before their first page request so an event between page-read and subscription cannot
    /// be missed; legacy callers may retain the non-blocking <see cref="Start"/> entry point.</summary>
    public async Task StartAsync(Guid conversationId, long afterSequence, CancellationToken ct)
    {
        Start(conversationId, afterSequence);
        await _connected.Task.WaitAsync(ct);
    }

    private async Task TailAsync(Guid conversationId, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await foreach (var evt in _hostClient.StreamEventsAsync(conversationId, _lastSequence,
                    () => _connected.TrySetResult(), ct))
                    await ApplyAsync(evt, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _connected.TrySetException(exception);
                _publish(new ClientRuntimeEvent(ClientRuntimeEventKind.Error, _sessionId,
                    Error: "Forge lost the durable conversation stream and is reconnecting."));
                // A normal SSE completion, protocol fault, or transient HTTP failure reconnects
                // from the last committed cursor. No retry-count/configuration knob exists.
            }

            try
            {
                await Task.Delay(ReconnectDelay, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    // A duplicate replay is harmless. A gap is a protocol fault: reconnect from the last durable
    // sequence instead of advancing over a fact that may contain a pending tool request. The hook
    // executes before the cursor advances, so a failed zero-authority refusal is retried.
    private async Task ApplyAsync(ConversationEvent evt, CancellationToken ct)
    {
        if (evt.Sequence <= _lastSequence)
            return;

        if (evt.Sequence != _lastSequence + 1)
        {
            _publish(new ClientRuntimeEvent(ClientRuntimeEventKind.Error, _sessionId,
                Error: $"Conversation protocol error: expected sequence {_lastSequence + 1}, received {evt.Sequence}."));
            throw new ConversationTailGapException();
        }

        if (_onEventAsync is not null)
            await _onEventAsync(evt, ct);

        _publish(new ClientRuntimeEvent(ClientRuntimeEventKind.ConversationEvent, _sessionId, Conversation: evt));
        _lastSequence = evt.Sequence;
    }

    public async ValueTask DisposeAsync()
    {
        await _lifetimeCts.CancelAsync();
        if (_tailTask is not null)
        {
            try
            {
                await _tailTask;
            }
            catch (OperationCanceledException)
            {
                // Expected — the tail loop observes cancellation and returns.
            }
        }

        _lifetimeCts.Dispose();
    }

    private static TaskCompletionSource NewConnectedSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

internal sealed class ConversationTailGapException : InvalidOperationException;
