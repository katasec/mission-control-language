using System.Text.Json;
using ForgeMission.ClientRuntime.Transport;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Core.Tools;
using Microsoft.Extensions.AI;

namespace ForgeMission.ClientRuntime.Services;

// Owns one Client Runtime-side durable Janus conversation: the start-vs-follow-up choice, the
// retained ConversationId/RunId, the background SSE tail (reconnecting from the last delivered
// sequence), and the expected-tool-request hand-off through the existing local capability
// authorization path. ConversationHostClient owns the wire projection; this class owns
// consequential session/retry/tool behaviour. Disposed by ClientRuntimeSessionStore when the
// owning Client Runtime session is replaced (a mission switch), and cancelled automatically on
// Client Runtime process shutdown via the linked application-stopping token — either path stops
// the tail before it can execute a later local tool for an abandoned session.
internal sealed class ConversationRuntimeSession : IAsyncDisposable
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromMilliseconds(250);

    private readonly string _sessionId;
    private readonly string _missionRef;
    private readonly ConversationHostClient _hostClient;
    private readonly CapabilityRegistry _capabilities;
    private readonly ICapabilityDispatcher _dispatcher;
    private readonly ToolExecutorRegistry _toolExecutors;
    private readonly Action<ClientRuntimeEvent> _publish;
    private readonly CancellationTokenSource _lifetimeCts;

    // Tail-loop-only state: touched exclusively by the single TailAsync loop, never concurrently
    // with a SendAsync call, so no additional locking is needed.
    private readonly HashSet<Guid> _seenEventIds = [];
    private readonly Dictionary<Guid, ToolExecutionResult> _toolResultCache = [];
    private long _lastSequence;

    private Guid? _conversationId;
    private Task? _tailTask;

    public ConversationRuntimeSession(
        string sessionId,
        string missionRef,
        ConversationHostClient hostClient,
        CapabilityRegistry capabilities,
        ICapabilityDispatcher dispatcher,
        Action<ClientRuntimeEvent> publish,
        CancellationToken applicationStopping,
        ToolExecutorRegistry? toolExecutors = null)
    {
        _sessionId = sessionId;
        _missionRef = missionRef;
        _hostClient = hostClient;
        _capabilities = capabilities;
        _dispatcher = dispatcher;
        _publish = publish;
        _toolExecutors = toolExecutors ?? new ToolExecutorRegistry();
        _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(applicationStopping);
    }

    // Returns the conversation's ID (starting it on the first call, submitting a follow-up
    // command against the retained ID on every later call). Throws HttpRequestException/
    // InvalidOperationException on failure, mirroring MissionRuntimeSession/
    // CloudMissionRuntimeSession's own SendAsync contract, so the endpoint's existing catch
    // handles both paths identically.
    public async Task<Guid> SendAsync(string prompt, CancellationToken ct)
    {
        if (_conversationId is null)
        {
            var response = await _hostClient.StartAsync(
                new StartConversationRequest(Guid.NewGuid(), _missionRef, prompt, ToCapabilityDeclarations(_capabilities)), ct);
            _conversationId = response.ConversationId;
            StartTailIfNeeded();
        }
        else
        {
            await _hostClient.SubmitCommandAsync(
                new SubmitConversationCommandRequest(_conversationId.Value, Guid.NewGuid(), prompt), ct);
        }

        return _conversationId.Value;
    }

    private void StartTailIfNeeded()
    {
        if (_tailTask is not null)
            return;

        _tailTask = Task.Run(() => TailAsync(_lifetimeCts.Token));
    }

    private async Task TailAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await foreach (var evt in _hostClient.StreamEventsAsync(_conversationId!.Value, _lastSequence, ct))
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

    // Relay-dedupe (by EventId, gating _publish) and tool-execution-idempotency (by RequestId,
    // gating the dispatcher call) are deliberately separate: an event the UI has already seen
    // must never render twice, but an expected tool request whose post never durably completed
    // must still be retryable without re-executing the local side effect.
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

        if (evt.Kind == ConversationEventKind.ToolRequested && evt.ToolRequest is not null)
            await HandleToolRequestedAsync(evt.ToolRequest, evt.Participant, ct);
        else if (evt.Kind == ConversationEventKind.ToolResult && evt.ToolResult is not null)
            _toolResultCache.Remove(evt.ToolResult.RequestId);
    }

    private async Task HandleToolRequestedAsync(
        ConversationToolRequest request, ConversationParticipant participant, CancellationToken ct)
    {
        if (!_toolResultCache.TryGetValue(request.RequestId, out var result))
        {
            var isExpected = participant == ConversationParticipant.Implementer
                && request.RequestId != Guid.Empty
                && !string.IsNullOrEmpty(request.ToolName)
                && _toolExecutors.CanExecute(request.ToolName);

            result = isExpected
                ? await ExecuteToolAsync(request, ct)
                : ToolExecutionResult.Error($"Unsupported or invalid tool request: {request.ToolName}");
            _toolResultCache[request.RequestId] = result;
        }

        try
        {
            await _hostClient.SubmitToolResultAsync(new SubmitToolResultRequest(
                _conversationId!.Value,
                ConversationDeterministicIds.ClientToolResult(request.RequestId),
                request.RequestId,
                result.Content,
                result.IsError), ct);
        }
        catch (HttpRequestException exception)
        {
            _publish(new ClientRuntimeEvent(ClientRuntimeEventKind.Error, _sessionId,
                Error: $"Failed to report the result for tool '{request.ToolName}': {exception.Message}"));
        }
    }

    private async Task<ToolExecutionResult> ExecuteToolAsync(ConversationToolRequest request, CancellationToken ct)
    {
        var call = new FunctionCallContent(request.RequestId.ToString("N"), request.ToolName, ToArguments(request.Arguments));
        return await _toolExecutors.ExecuteAsync(call, _dispatcher, ct);
    }

    private static ConversationCapabilityDeclaration[] ToCapabilityDeclarations(CapabilityRegistry capabilities) =>
        capabilities.ToolDeclarations.Select(tool =>
        {
            var function = (AIFunction)tool;
            return new ConversationCapabilityDeclaration(function.Name, function.Description, function.JsonSchema);
        }).ToArray();

    private static IDictionary<string, object?> ToArguments(JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object)
            return new Dictionary<string, object?>();

        return arguments.EnumerateObject().ToDictionary(
            property => property.Name,
            property => ToObject(property.Value),
            StringComparer.Ordinal);
    }

    private static object? ToObject(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number => value.GetDouble(),
        JsonValueKind.Null => null,
        _ => value.Clone(),
    };

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
