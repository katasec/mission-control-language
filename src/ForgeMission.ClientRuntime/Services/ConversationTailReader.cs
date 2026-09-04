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
    // with a send, so no additional locking is needed.
    private readonly HashSet<Guid> _seenEventIds = [];
    private long _lastSequence;

    private Task? _tailTask;

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

    /// <summary>Starts following <paramref name="conversationId"/> from sequence 0, so a reopened
    /// conversation replays its whole durable history before live events arrive. Idempotent: a
    /// second call while a tail is already running is a no-op.</summary>
    public void Start(Guid conversationId)
    {
        if (_tailTask is not null)
            return;

        _tailTask = Task.Run(() => TailAsync(conversationId, _lifetimeCts.Token));
    }

    private async Task TailAsync(Guid conversationId, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await foreach (var evt in _hostClient.StreamEventsAsync(conversationId, _lastSequence, ct))
                    await ApplyAsync(evt, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                // Normal SSE completion or a transient HTTP failure — reconnect below with the
                // same cursor. No retry-count/configuration knob: this is a fixed policy.
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

    // Relay-dedupe by EventId gates _publish, so an event the UI has already seen never renders
    // twice across a replay/live overlap or a reconnect. The hook runs regardless of that dedupe:
    // its own idempotency is its consumer's concern, because a side effect whose report never
    // durably landed must still be retryable.
    private async Task ApplyAsync(ConversationEvent evt, CancellationToken ct)
    {
        if (_seenEventIds.Add(evt.EventId))
        {
            if (evt.Sequence <= _lastSequence)
            {
                _publish(new ClientRuntimeEvent(ClientRuntimeEventKind.Error, _sessionId,
                    Error: $"Conversation protocol error: received unseen event at sequence {evt.Sequence}, already past {_lastSequence}."));
            }
            else
            {
                _lastSequence = evt.Sequence;
                _publish(new ClientRuntimeEvent(ClientRuntimeEventKind.ConversationEvent, _sessionId, Conversation: evt));
            }
        }

        if (_onEventAsync is not null)
            await _onEventAsync(evt, ct);
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
}
