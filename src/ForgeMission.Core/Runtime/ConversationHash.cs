using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;

namespace ForgeMission.Core.Runtime;

// Content-addressed conversation identity (Phase 42.3 §3/§4). Hashes a CANONICALIZED
// projection — roles + text + tool blocks — never raw request bytes, so field order,
// whitespace, and client-added metadata don't split the identity.
//
//   P = prefix through the last real user turn  → enrichment cache key (enrich once per turn)
//   F = the full message array                  → duplicate_continuation detection
public static class ConversationHash
{
    public static string Prefix(IReadOnlyList<ChatMessage> messages)
        => Hash(messages.Take(LastUserTurnIndex(messages) + 1));

    public static string Full(IReadOnlyList<ChatMessage> messages)
        => Hash(messages);

    // The last message that is a REAL user turn: user role, carries text, is not a
    // tool_result hand-back (those also arrive with role "user" on the Anthropic wire).
    private static int LastUserTurnIndex(IReadOnlyList<ChatMessage> messages)
    {
        for (var i = messages.Count - 1; i >= 0; i--)
        {
            var m = messages[i];
            if (m.Role == ChatRole.User
                && m.Contents.OfType<TextContent>().Any()
                && !m.Contents.OfType<FunctionResultContent>().Any())
                return i;
        }
        return messages.Count - 1;
    }

    private static string Hash(IEnumerable<ChatMessage> messages)
    {
        // \u001e separates messages, \u001f separates parts - unambiguous, content-safe.
        var sb = new StringBuilder();
        foreach (var message in messages)
        {
            sb.Append('\u001e').Append(message.Role.Value);
            foreach (var part in message.Contents)
            {
                switch (part)
                {
                    case TextContent text:
                        sb.Append('\u001f').Append("text:").Append(text.Text);
                        break;
                    case FunctionCallContent call:
                        sb.Append('\u001f').Append("tool_use:").Append(call.CallId).Append(':').Append(call.Name);
                        break;
                    case FunctionResultContent result:
                        sb.Append('\u001f').Append("tool_result:").Append(result.CallId).Append(':').Append(result.Result);
                        break;
                }
            }
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }
}
