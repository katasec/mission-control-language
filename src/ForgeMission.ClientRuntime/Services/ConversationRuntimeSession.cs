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
    private readonly string _missionRef;
    private readonly ConversationHostClient _hostClient;
    private readonly ConversationToolHandOff _tools;
    private readonly ConversationTailReader _tail;

    private Guid? _conversationId;

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
        _missionRef = missionRef;
        _hostClient = hostClient;
        _tools = new ConversationToolHandOff(sessionId, hostClient, capabilities, dispatcher, publish, toolExecutors);
        // The tool hand-off is supplied to the shared tail reader as its per-event hook. A
        // Project-control session supplies none (43.20 task 2).
        _tail = new ConversationTailReader(sessionId, hostClient, publish, applicationStopping, OnTailEventAsync);
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
                new StartConversationRequest(Guid.NewGuid(), _missionRef, prompt, _tools.Declarations), ct);
            _conversationId = response.ConversationId;
            _tail.Start(_conversationId.Value);
        }
        else
        {
            await _hostClient.SubmitCommandAsync(
                new SubmitConversationCommandRequest(_conversationId.Value, Guid.NewGuid(), prompt), ct);
        }

        return _conversationId.Value;
    }

    // The tail reader owns replay, reconnect and relay-dedupe; ConversationToolHandOff owns what
    // to do with a tool request once one is durably observed.
    private Task OnTailEventAsync(ConversationEvent evt, CancellationToken ct) =>
        _tools.OnTailEventAsync(_conversationId!.Value, evt, ct);

    public ValueTask DisposeAsync() => _tail.DisposeAsync();
}
