using Microsoft.Extensions.AI;

namespace ForgeMission.Core.Runtime;

// The full client conversation, carried through the context bag as a real object (Phase 42.1).
// Structure stays first-class — consumers that need roles, parts, or tool shapes read Messages;
// template interpolation ({{conversation}}) gets a readable transcript via ToString().
// Do not replace this with a flattened string: tool calls, multimodal parts, and role
// (prompt-injection) boundaries must survive for 42.3.
public sealed class Conversation(IReadOnlyList<ChatMessage> messages)
{
    public IReadOnlyList<ChatMessage> Messages { get; } = messages;

    public override string ToString()
        => string.Join("\n\n", Messages.Select(RenderMessage));

    private static string RenderMessage(ChatMessage message)
        => $"{message.Role.Value}: {string.Join("\n", RenderParts(message))}";

    private static IEnumerable<string> RenderParts(ChatMessage message)
    {
        foreach (var part in message.Contents)
        {
            switch (part)
            {
                case TextContent text:
                    yield return text.Text;
                    break;
                case FunctionCallContent call:
                    yield return $"[tool_use: {call.Name}]";
                    break;
                case FunctionResultContent result:
                    yield return $"[tool_result: {result.Result}]";
                    break;
            }
        }
    }
}
