using System.Text.Json;
using ForgeMission.ClientRuntime.Transport;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Core.Tools;
using Microsoft.Extensions.AI;

namespace ForgeMission.ClientRuntime.Services;

/// <summary>
/// The one owner of "a durable run asked for a local tool" (extracted for 43.21 task 1, behaviour
/// unchanged from the Janus session it came out of).
///
/// It exists as its own class because there are now two durable session types that host tool-using
/// runs — the legacy Janus <see cref="ConversationRuntimeSession"/> and the Project's
/// <see cref="ProjectMissionRuntimeSession"/> — and a second copy of this logic is exactly where
/// the two would drift on which requests are honoured. Every rule below is a refusal, so having
/// one copy is a correctness property, not tidiness.
///
/// It never decides what a run does; it only executes an already-authorized capability and reports
/// the outcome back through the Conversation service.
/// </summary>
internal sealed class ConversationToolHandOff(
    string sessionId,
    ConversationHostClient hostClient,
    CapabilityRegistry capabilities,
    ICapabilityDispatcher dispatcher,
    Action<ClientRuntimeEvent> publish,
    ToolExecutorRegistry? toolExecutors = null)
{
    private readonly ToolExecutorRegistry _toolExecutors = toolExecutors ?? new ToolExecutorRegistry();

    // Tail-loop-only state: touched exclusively by the tail reader's single loop, never
    // concurrently with a submission, so no additional locking is needed. It exists so a replayed
    // ToolRequested re-reports the SAME result rather than executing the tool a second time.
    private readonly Dictionary<Guid, ToolExecutionResult> _toolResultCache = [];

    /// <summary>The capability set this session offers a run, in the wire shape the Conversation
    /// service expects.</summary>
    public ConversationCapabilityDeclaration[] Declarations =>
        [.. capabilities.ToolDeclarations.Select(tool =>
        {
            var function = (AIFunction)tool;
            return new ConversationCapabilityDeclaration(function.Name, function.Description, function.JsonSchema);
        })];

    /// <summary>The per-event hook a durable tail supplies. It acts on exactly two event kinds and
    /// ignores everything else — a session that passes this hook gains a tool hand-off and nothing
    /// more.</summary>
    public async Task OnTailEventAsync(Guid conversationId, ConversationEvent evt, CancellationToken ct)
    {
        if (evt.Kind == ConversationEventKind.ToolRequested && evt.ToolRequest is not null)
            await HandleToolRequestedAsync(conversationId, evt.ToolRequest, evt.Participant, ct);
        else if (evt.Kind == ConversationEventKind.ToolResult && evt.ToolResult is not null)
            _toolResultCache.Remove(evt.ToolResult.RequestId);
    }

    private async Task HandleToolRequestedAsync(
        Guid conversationId, ConversationToolRequest request, ConversationParticipant participant, CancellationToken ct)
    {
        if (!_toolResultCache.TryGetValue(request.RequestId, out var result))
        {
            // Every clause is a refusal. Only Janus's Implementer may request a tool at all, and
            // only one this Runtime actually knows how to execute — an unexpected request is
            // answered with an error rather than executed or silently dropped, so the run learns
            // its request was refused instead of hanging.
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
            await hostClient.SubmitToolResultAsync(new SubmitToolResultRequest(
                conversationId,
                ConversationDeterministicIds.ClientToolResult(request.RequestId),
                request.RequestId,
                result.Content,
                result.IsError), ct);
        }
        catch (HttpRequestException exception)
        {
            publish(new ClientRuntimeEvent(ClientRuntimeEventKind.Error, sessionId,
                Error: $"Failed to report the result for tool '{request.ToolName}': {exception.Message}"));
        }
    }

    private async Task<ToolExecutionResult> ExecuteToolAsync(ConversationToolRequest request, CancellationToken ct)
    {
        var call = new FunctionCallContent(request.RequestId.ToString("N"), request.ToolName, ToArguments(request.Arguments));
        return await _toolExecutors.ExecuteAsync(call, dispatcher, ct);
    }

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
}
