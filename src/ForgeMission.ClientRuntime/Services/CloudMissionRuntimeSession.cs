using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ForgeMission.Core.Runtime;
using ForgeMission.Core.Tools;
using Microsoft.Extensions.AI;

namespace ForgeMission.ClientRuntime.Services;

/// <summary>
/// Drives Forge API A's buffered ExecuteMission tool round-trip. The client owns the transcript:
/// ForgeAPI is stateless between calls, so tool continuations replay every prior turn locally.
/// </summary>
public sealed partial class CloudMissionRuntimeSession(
    HttpClient httpClient, string mission = CloudMissionRuntimeSession.DefaultMission, ToolExecutorRegistry? toolExecutors = null)
{
    private const string DefaultMission = "vanilla";

    private readonly ToolExecutorRegistry _toolExecutors = toolExecutors ?? new ToolExecutorRegistry();
    private readonly List<WireTurnMessage> _history = [];

    public async Task<string> SendAsync(
        string prompt,
        CapabilityRegistry capabilities,
        ICapabilityDispatcher dispatcher,
        Action<string>? onTextDelta = null,
        Action<ToolCallNotification>? onToolCall = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var clientToken = Guid.NewGuid().ToString("N");
        var firstTurn = _history.Count == 0;
        if (!firstTurn)
            _history.Add(WireTurnMessage.UserText(prompt));

        while (true)
        {
            var response = await SendTurnAsync(prompt, clientToken, capabilities, ct);
            ThrowIfFailed(response);

            if (response.ToolUse is not { Count: > 0 } calls)
            {
                if (firstTurn)
                    _history.Add(WireTurnMessage.UserText(prompt));
                _history.Add(WireTurnMessage.AssistantText(response.Answer));
                if (!string.IsNullOrEmpty(response.Answer))
                    onTextDelta?.Invoke(response.Answer);
                return response.Answer;
            }

            if (firstTurn)
                _history.Add(WireTurnMessage.UserText(prompt));
            _history.Add(WireTurnMessage.AssistantToolUse(calls));

            var results = new List<WireTurnContent>();
            foreach (var call in calls)
            {
                var functionCall = new FunctionCallContent(call.Id, call.Name, ToArguments(call.Arguments));
                onToolCall?.Invoke(new ToolCallNotification(functionCall, ToolCallNotificationState.Running));

                var result = await _toolExecutors.ExecuteAsync(functionCall, dispatcher, ct);
                onToolCall?.Invoke(new ToolCallNotification(functionCall, ToolCallNotificationState.Done, result));

                results.Add(WireTurnContent.ToolResultBlock(call.Id, result.Content));
            }

            _history.Add(new WireTurnMessage("user", results));
            firstTurn = false;
        }
    }

    private async Task<WireExecuteMissionResponse> SendTurnAsync(
        string prompt,
        string clientToken,
        CapabilityRegistry capabilities,
        CancellationToken ct)
    {
        var request = new WireExecuteMission
        {
            Version = 1,
            ClientToken = clientToken,
            Mission = mission,
            Input = prompt,
            Stream = false,
            History = _history.Count == 0 ? null : _history,
            Tools = capabilities.ToolDeclarations.Select(ToWireTool).ToList(),
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "api/ExecuteMission")
        {
            Content = JsonContent.Create(request, CloudWireJsonContext.Default.WireExecuteMission),
        };
        using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Forge API returned HTTP {(int)response.StatusCode}: {body}");
        }

        return await response.Content.ReadFromJsonAsync(
                CloudWireJsonContext.Default.WireExecuteMissionResponse,
                ct)
            ?? throw new InvalidOperationException("Forge API returned no ExecuteMission response.");
    }

    private static void ThrowIfFailed(WireExecuteMissionResponse response)
    {
        if (!string.IsNullOrWhiteSpace(response.ResponseStatus?.ErrorCode))
            throw new InvalidOperationException(
                response.ResponseStatus.Message ?? response.ResponseStatus.ErrorCode);
    }

    private static WireMissionTool ToWireTool(AITool tool)
    {
        var function = (AIFunction)tool;
        return new WireMissionTool(function.Name, function.Description, function.JsonSchema);
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

    private sealed class WireExecuteMission
    {
        public int Version { get; init; }
        public string ClientToken { get; init; } = "";
        public string Mission { get; init; } = "";
        public string Input { get; init; } = "";
        public bool Stream { get; init; }
        public List<WireTurnMessage>? History { get; init; }
        public List<WireMissionTool>? Tools { get; init; }
    }

    private sealed class WireExecuteMissionResponse
    {
        public string Answer { get; init; } = "";
        public List<WireToolUseCall>? ToolUse { get; init; }
        public WireResponseStatus? ResponseStatus { get; init; }
    }

    private sealed record WireTurnMessage(string Role, List<WireTurnContent> Content)
    {
        public static WireTurnMessage UserText(string text) =>
            new("user", [new WireTurnContent("text", Text: text)]);

        public static WireTurnMessage AssistantText(string text) =>
            new("assistant", [new WireTurnContent("text", Text: text)]);

        public static WireTurnMessage AssistantToolUse(IEnumerable<WireToolUseCall> calls) =>
            new("assistant", calls.Select(call => new WireTurnContent(
                "tool_use", ToolUseId: call.Id, ToolName: call.Name, ToolInput: call.Arguments)).ToList());
    }

    private sealed record WireTurnContent(
        string Type,
        string? Text = null,
        string? ToolUseId = null,
        string? ToolName = null,
        JsonElement? ToolInput = null,
        string? ToolResult = null)
    {
        public static WireTurnContent ToolResultBlock(string id, string result) =>
            new("tool_result", ToolUseId: id, ToolResult: result);
    }

    private sealed record WireMissionTool(string Name, string Description, JsonElement InputSchema);
    private sealed record WireToolUseCall(string Id, string Name, JsonElement Arguments);
    private sealed record WireResponseStatus(string? ErrorCode, string? Message);

    [JsonSourceGenerationOptions(
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(WireExecuteMission))]
    [JsonSerializable(typeof(WireExecuteMissionResponse))]
    private sealed partial class CloudWireJsonContext : JsonSerializerContext;
}
