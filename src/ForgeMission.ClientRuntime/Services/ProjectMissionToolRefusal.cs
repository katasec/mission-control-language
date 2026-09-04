using ForgeMission.ClientRuntime.Transport;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ClientRuntime.Services;

/// <summary>
/// What a Project Mission Run does when something asks for a local tool: refuses it, in writing,
/// and never executes anything (43.21 task 1).
///
/// Starting a Project Mission Run grants no local tool authority. Both missions are declared zero
/// capabilities, so a tool request on this route should be impossible — but "should be impossible"
/// is not a plan for what happens if it occurs. Two outcomes are unacceptable: executing it, and
/// leaving the run stuck in WaitingForTool forever. So the request is answered with an error
/// result, which lets the run reach a terminal status truthfully.
///
/// The refusal is STRUCTURAL, not a decision this class makes. Compare it with
/// <see cref="ConversationToolHandOff"/>, which the legacy Janus path still uses: that one holds a
/// <c>CapabilityRegistry</c>, an <c>ICapabilityDispatcher</c> and a <c>ToolExecutorRegistry</c>.
/// This one holds none of them and takes none in its constructor, so there is no expression
/// anywhere in it that could reach a local executor — not because a branch declines to, but
/// because the machinery is absent.
/// </summary>
internal sealed class ProjectMissionToolRefusal(
    string sessionId,
    ConversationHostClient hostClient,
    Action<ClientRuntimeEvent> publish)
{
    /// <summary>The per-event hook the durable tail supplies. It acts on exactly one event kind
    /// and ignores every other.</summary>
    public async Task OnTailEventAsync(Guid containerId, ConversationEvent evt, CancellationToken ct)
    {
        if (evt.Kind != ConversationEventKind.ToolRequested || evt.ToolRequest is not { } request)
            return;

        // Surfaced as well as answered: a mission asking for a tool it was never offered means the
        // mission asset and this route disagree, and that should be visible rather than only
        // resolved quietly in the transcript.
        publish(new ClientRuntimeEvent(ClientRuntimeEventKind.Error, sessionId,
            Error: $"A mission run requested the tool '{request.ToolName}'. Project mission runs have no local " +
                   "tool access, so the request was refused."));

        try
        {
            await hostClient.SubmitToolResultAsync(new SubmitToolResultRequest(
                containerId,
                // The same deterministic id the authorized path uses, so a redelivered request
                // resolves to this one refusal rather than appending a second result.
                ConversationDeterministicIds.ClientToolResult(request.RequestId),
                request.RequestId,
                "This mission run has no local tool access. The request was refused and nothing was executed.",
                IsError: true), ct);
        }
        catch (HttpRequestException exception)
        {
            publish(new ClientRuntimeEvent(ClientRuntimeEventKind.Error, sessionId,
                Error: $"Failed to report the refusal of tool '{request.ToolName}': {exception.Message}"));
        }
    }
}
