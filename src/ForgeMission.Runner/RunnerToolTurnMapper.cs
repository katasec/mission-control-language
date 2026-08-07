using System.Buffers;
using System.Text.Json;
using ForgeMission.Core.Runtime;
using ForgeMission.Runner.Contracts;
using Katasec.AITools;
using Microsoft.Extensions.AI;

namespace ForgeMission.Runner;

/// <summary>Reflection-free translation between API-A agent-turn DTOs and the Core runtime.</summary>
internal static class RunnerToolTurnMapper
{
    public static IReadOnlyList<ChatMessage> ToChatMessages(
        IReadOnlyList<TurnMessage>? history,
        string goal)
    {
        if (history is not { Count: > 0 })
            return [new ChatMessage(ChatRole.User, goal)];

        return history.Select(ToChatMessage).ToList();
    }

    public static IReadOnlyDictionary<string, object> ToContextObjects(IReadOnlyList<ChatMessage> messages)
        => new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["conversation"] = new Conversation(messages),
            ["system"] = string.Join("\n\n", messages.Where(m => m.Role == ChatRole.System).Select(m => m.Text)),
        };

    public static IList<AITool>? ToTools(IReadOnlyList<MissionToolDecl>? tools)
        => tools?.Select(tool => (AITool)new DeclaredTool(
                tool.Name,
                tool.Description,
                tool.InputSchema))
            .ToList();

    public static IReadOnlyList<ToolUseCall>? ToToolUse(IReadOnlyList<FunctionCallContent>? calls)
    {
        if (calls is not { Count: > 0 })
            return null;

        return calls.Select(call => new ToolUseCall
        {
            Id = string.IsNullOrWhiteSpace(call.CallId)
                ? throw new InvalidOperationException("A tool call must include an id.")
                : call.CallId,
            Name = call.Name,
            Arguments = ToJsonElement(call.Arguments ?? new Dictionary<string, object?>()),
        }).ToList();
    }

    // A tool-result hand-back always arrives as its own homogeneous "user" turn on the wire (the
    // client's own bookkeeping — see TurnMessage.Role's "user"|"assistant" contract), but that is
    // NOT the role every provider expects on the wire it actually speaks: Anthropic's API genuinely
    // accepts tool_result content inside a user-role message, but OpenAI's Chat Completions API
    // rejects that outright (HTTP 400 "An assistant message with 'tool_calls' must be followed by
    // tool messages...") — it requires a dedicated role="tool" message. Microsoft.Extensions.AI's
    // own abstraction has exactly this seam (ChatRole.Tool, already used the same way in
    // AgenticSession.cs) specifically so each provider's adapter can translate it correctly — the
    // wire's "user" is about who sent it, not what role the underlying LLM call should see.
    // Confirmed live 2026-08-08 (real gpt-4o-mini 400) that mapping tool_result 1:1 into
    // ChatRole.User, as an earlier revision of this mapper did, is provider-specific-wrong.
    private static ChatMessage ToChatMessage(TurnMessage message)
    {
        var contents = message.Content.Select(ToContent).ToList();
        var role = contents.All(c => c is FunctionResultContent) && contents.Count > 0
            ? ChatRole.Tool
            : ToRole(message.Role);
        return new ChatMessage(role, contents);
    }

    private static ChatRole ToRole(string role) => role switch
    {
        "user" => ChatRole.User,
        "assistant" => ChatRole.Assistant,
        _ => throw new ArgumentException($"Unknown conversation role '{role}'.", nameof(role)),
    };

    private static AIContent ToContent(TurnContent content) => content.Type switch
    {
        "text" => new TextContent(content.Text ?? string.Empty),
        "tool_use" => new FunctionCallContent(
            Require(content.ToolUseId, "tool_use id"),
            Require(content.ToolName, "tool_use name"),
            ToArguments(content.ToolInput)),
        "tool_result" => new FunctionResultContent(
            Require(content.ToolUseId, "tool_result id"),
            content.ToolResult ?? string.Empty),
        _ => throw new ArgumentException($"Unknown conversation content type '{content.Type}'.", nameof(content)),
    };

    private static string Require(string? value, string field) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"A {field} is required.")
            : value;

    // Mirrors MissionRuntimeSession's JSON-element conversion in the opposite direction. The
    // canonical values below are the only values this mapper creates, so no reflection serializer
    // is needed (or permitted in the Native AOT runner).
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

    private static JsonElement ToJsonElement(IDictionary<string, object?> arguments)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartObject();
        foreach (var (name, value) in arguments)
        {
            writer.WritePropertyName(name);
            WriteValue(writer, value);
        }
        writer.WriteEndObject();
        writer.Flush();

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                return;
            case string text:
                writer.WriteStringValue(text);
                return;
            case bool boolean:
                writer.WriteBooleanValue(boolean);
                return;
            case long integer:
                writer.WriteNumberValue(integer);
                return;
            case double number:
                writer.WriteNumberValue(number);
                return;
            case JsonElement element:
                element.WriteTo(writer);
                return;
            default:
                throw new InvalidOperationException(
                    $"Unsupported tool argument type '{value.GetType().FullName}'.");
        }
    }
}
