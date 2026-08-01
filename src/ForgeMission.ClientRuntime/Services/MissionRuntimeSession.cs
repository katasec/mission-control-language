using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ForgeMission.Core.Runtime;
using ForgeMission.Core.Tools;
using Microsoft.Extensions.AI;

namespace ForgeMission.ClientRuntime.Services;

// Drives the existing Anthropic /v1/messages tool-round-trip from the local Client Runtime.
// The Mission Runtime remains responsible for deciding which tool to call; this class only
// executes that request against the user's selected workspace and returns its result over the
// already-established wire contract.
// ChatGPT is the public runner's vanilla built-in label. A MissionRef runner has one loaded mission
// and therefore ignores this model id through its single-mission fallback.
public sealed class MissionRuntimeSession(HttpClient httpClient, string model = "ChatGPT", ToolExecutorRegistry? toolExecutors = null)
{
    private readonly ToolExecutorRegistry _toolExecutors = toolExecutors ?? new ToolExecutorRegistry();
    private readonly List<WireMessage> _messages = [];

    public async Task<string> SendAsync(
        string prompt,
        CapabilityRegistry capabilities,
        ICapabilityDispatcher dispatcher,
        Action<string>? onTextDelta = null,
        Action<ToolCallNotification>? onToolCall = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        _messages.Add(WireMessage.UserText(prompt));

        while (true)
        {
            var response = await SendTurnAsync(capabilities, onTextDelta, ct);
            _messages.Add(new WireMessage("assistant", response.Content));

            if (response.ToolCalls.Count == 0)
                return response.Text;

            var results = new List<WireContentBlock>();
            foreach (var call in response.ToolCalls)
            {
                var functionCall = new FunctionCallContent(call.Id, call.Name, call.Arguments);
                onToolCall?.Invoke(new ToolCallNotification(functionCall, ToolCallNotificationState.Running));

                var result = await _toolExecutors.ExecuteAsync(functionCall, dispatcher, ct);
                onToolCall?.Invoke(new ToolCallNotification(functionCall, ToolCallNotificationState.Done, result));

                results.Add(WireContentBlock.ToolResult(call.Id, result.Content));
            }

            _messages.Add(new WireMessage("user", ToElement(results)));
        }
    }

    private async Task<TurnResponse> SendTurnAsync(
        CapabilityRegistry capabilities,
        Action<string>? onTextDelta,
        CancellationToken ct)
    {
        var request = new WireRequest
        {
            Model = model,
            Messages = _messages,
            Stream = true,
            Tools = capabilities.ToolDeclarations.Select(ToWireTool).ToList(),
        };
        var json = JsonSerializer.Serialize(request);

        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Mission Runtime returned HTTP {(int)response.StatusCode}: {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        var parser = new AnthropicSseParser(onTextDelta);

        string? eventName = null;
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (line.Length == 0)
            {
                parser.CompleteEvent(eventName);
                eventName = null;
            }
            else if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                eventName = line["event: ".Length..];
            }
            else if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                parser.AddData(line["data: ".Length..]);
            }
        }

        parser.CompleteEvent(eventName);
        return parser.BuildResponse();
    }

    private static WireTool ToWireTool(AITool tool)
    {
        var function = (AIFunction)tool;
        return new WireTool(function.Name, function.Description, function.JsonSchema);
    }

    private static JsonElement ToElement<T>(T value) => JsonSerializer.SerializeToElement(value);

    private sealed class AnthropicSseParser(Action<string>? onTextDelta)
    {
        private readonly Dictionary<int, WireContentBlock> _blocks = [];
        private readonly Dictionary<int, StringBuilder> _toolArguments = [];
        private readonly StringBuilder _eventData = new();
        private readonly StringBuilder _text = new();

        public void AddData(string data) => _eventData.Append(data);

        public void CompleteEvent(string? eventName)
        {
            if (eventName is null || _eventData.Length == 0)
                return;

            using var document = JsonDocument.Parse(_eventData.ToString());
            _eventData.Clear();

            switch (eventName)
            {
                case "content_block_start":
                    StartBlock(document.RootElement);
                    break;
                case "content_block_delta":
                    AddDelta(document.RootElement);
                    break;
                case "content_block_stop":
                    FinishBlock(document.RootElement);
                    break;
            }
        }

        public TurnResponse BuildResponse()
        {
            var content = _blocks.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList();
            var calls = content
                .Where(block => block.Type == "tool_use")
                .Select(block => new ToolCall(
                    block.Id ?? throw new InvalidOperationException("Mission Runtime returned a tool call without an id."),
                    block.Name ?? throw new InvalidOperationException("Mission Runtime returned a tool call without a name."),
                    ToArguments(block.Input)))
                .ToList();

            return new TurnResponse(ToElement(content), _text.ToString(), calls);
        }

        private void StartBlock(JsonElement root)
        {
            var index = root.GetProperty("index").GetInt32();
            var block = root.GetProperty("content_block");
            var type = block.GetProperty("type").GetString();

            if (type == "text")
                _blocks[index] = WireContentBlock.TextBlock(string.Empty);
            else if (type == "tool_use")
                _blocks[index] = WireContentBlock.ToolUse(
                    block.GetProperty("id").GetString() ?? string.Empty,
                    block.GetProperty("name").GetString() ?? string.Empty);
        }

        private void AddDelta(JsonElement root)
        {
            var index = root.GetProperty("index").GetInt32();
            var delta = root.GetProperty("delta");
            var type = delta.GetProperty("type").GetString();

            if (type == "text_delta")
            {
                var text = delta.GetProperty("text").GetString() ?? string.Empty;
                _text.Append(text);
                onTextDelta?.Invoke(text);
                return;
            }

            if (type == "input_json_delta")
            {
                if (!_toolArguments.TryGetValue(index, out var arguments))
                {
                    arguments = new StringBuilder();
                    _toolArguments[index] = arguments;
                }

                arguments.Append(delta.GetProperty("partial_json").GetString());
            }
        }

        private void FinishBlock(JsonElement root)
        {
            var index = root.GetProperty("index").GetInt32();
            if (!_blocks.TryGetValue(index, out var block) || block.Type != "tool_use")
                return;

            var input = _toolArguments.TryGetValue(index, out var arguments)
                ? JsonDocument.Parse(arguments.ToString()).RootElement.Clone()
                : JsonSerializer.SerializeToElement(new { });
            _blocks[index] = block with { Input = input };
        }

        private static IDictionary<string, object?> ToArguments(JsonElement? input)
        {
            if (input is not { ValueKind: JsonValueKind.Object } objectInput)
                return new Dictionary<string, object?>();

            return objectInput.EnumerateObject().ToDictionary(
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

    private sealed record TurnResponse(JsonElement Content, string Text, List<ToolCall> ToolCalls);
    private sealed record ToolCall(string Id, string Name, IDictionary<string, object?> Arguments);

    private sealed class WireRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; init; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<WireMessage> Messages { get; init; } = [];

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; init; } = 1024;

        [JsonPropertyName("stream")]
        public bool Stream { get; init; }

        [JsonPropertyName("tools")]
        public List<WireTool> Tools { get; init; } = [];
    }

    private sealed record WireTool(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("input_schema")] JsonElement InputSchema);

    private sealed record WireMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] JsonElement Content)
    {
        public static WireMessage UserText(string text) => new("user", ToElement(new List<WireContentBlock>
        {
            WireContentBlock.TextBlock(text),
        }));
    }

    private sealed record WireContentBlock(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Text = null,
        [property: JsonPropertyName("id")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Id = null,
        [property: JsonPropertyName("name")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Name = null,
        [property: JsonPropertyName("input")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonElement? Input = null,
        [property: JsonPropertyName("tool_use_id")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ToolUseId = null,
        [property: JsonPropertyName("content")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ResultContent = null)
    {
        public static WireContentBlock TextBlock(string text) => new("text", Text: text);
        public static WireContentBlock ToolUse(string id, string name) => new("tool_use", Id: id, Name: name);
        public static WireContentBlock ToolResult(string id, string content) => new("tool_result", ToolUseId: id, ResultContent: content);
    }
}
