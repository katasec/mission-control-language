namespace ForgeMission.Rooms;

/// <summary>
/// The pull gate (tenet 2 — pull, never push): an agent runs only when @-addressed. Pure
/// domain logic — given a message and the room's agent handles, returns the addressed handle
/// + the extracted prompt, or null when no agent is addressed (the message is just chat).
/// </summary>
public static class MentionParser
{
    public static Mention? Detect(string text, IEnumerable<string> agentHandles)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // Longest handle first, so "@a/b-c" wins over a shorter overlapping "@a".
        foreach (var handle in agentHandles.OrderByDescending(h => h.Length))
        {
            if (string.IsNullOrEmpty(handle))
                continue;

            var idx = text.IndexOf(handle, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                continue;

            var prompt = (text[..idx] + text[(idx + handle.Length)..])
                .Trim()
                .Trim('"', '\'', '“', '”', ':', ' ')
                .Trim();

            return new Mention(handle, prompt);
        }

        return null;
    }
}

public readonly record struct Mention(string Handle, string Prompt);
