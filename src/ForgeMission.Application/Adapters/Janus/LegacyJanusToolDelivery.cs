using System.Text.Json;
using ForgeMission.Application.Transport;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Core.Tools;
using Microsoft.Extensions.AI;

namespace ForgeMission.Application;

/// <summary>Compatibility adapter for Janus tool events. It validates the legacy Janus
/// participant/protocol shape, then asks Bob to decide and execute the local operation.</summary>
internal sealed class LegacyJanusToolDelivery(
    string sessionId,
    ConversationHostClient hostClient,
    ICapabilityDispatcher dispatcher,
    Action<ApplicationEvent> publish,
    ToolExecutorRegistry? toolExecutors = null)
{
    private readonly ToolExecutorRegistry _toolExecutors = toolExecutors ?? new ToolExecutorRegistry();
    private readonly Dictionary<Guid, ToolExecutionResult> _resultCache = [];

    public async Task ApplyAsync(ConversationEvent evt, Guid conversationId, CancellationToken ct)
    {
        if (evt.Kind == ConversationEventKind.ToolResult && evt.ToolResult is not null)
        {
            _resultCache.Remove(evt.ToolResult.RequestId);
            return;
        }

        if (evt.Kind != ConversationEventKind.ToolRequested || evt.ToolRequest is not { } request)
            return;

        var result = await ResolveResultAsync(request, evt.Participant, ct);
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
            // Preserve legacy behavior: reporting failure is visible, but does not make the
            // tail retry this event or claim a durable acknowledgement.
            publish(new ApplicationEvent(ApplicationEventKind.Error, sessionId,
                Error: $"Failed to report the result for tool '{request.ToolName}': {exception.Message}"));
        }
    }

    private async Task<ToolExecutionResult> ResolveResultAsync(
        ConversationToolRequest request, ConversationParticipant participant, CancellationToken ct)
    {
        if (_resultCache.TryGetValue(request.RequestId, out var cached))
            return cached;

        var isExpected = participant == ConversationParticipant.Implementer
            && request.RequestId != Guid.Empty
            && !string.IsNullOrEmpty(request.ToolName)
            && _toolExecutors.CanExecute(request.ToolName);
        var result = isExpected
            ? await ExecuteAsync(request, ct)
            : ToolExecutionResult.Error($"Unsupported or invalid tool request: {request.ToolName}");
        _resultCache[request.RequestId] = result;
        return result;
    }

    private Task<ToolExecutionResult> ExecuteAsync(ConversationToolRequest request, CancellationToken ct)
    {
        var call = new FunctionCallContent(request.RequestId.ToString("N"), request.ToolName, ToArguments(request.Arguments));
        return _toolExecutors.ExecuteAsync(call, dispatcher, ct);
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
