namespace ForgeMission.ConversationHost.Grains;

/// <summary>
/// The Host's one rule for a Mission Chat's durable display title (Phase 48). It is a pure
/// normalization of the conversation's first user message: no model is called, no mission name is
/// consulted, and nothing about the pinned launch reaches it. <see cref="Default"/> is what a
/// conversation projects before its first turn.
/// </summary>
internal static class MissionChatTitle
{
    /// <summary>What a conversation with no accepted turn is called. The words live in the shared
    /// conversation vocabulary so Host and its callers never hold two copies of them.</summary>
    internal const string Default = ForgeMission.Conversations.Contracts.MissionConversationTitles.Default;

    private const int MaxLength = 60;

    /// <summary>Trims, collapses internal whitespace, drops control characters, and cuts to
    /// <see cref="MaxLength"/> characters on a word boundary when one is available. Text that
    /// leaves nothing printable falls back to <see cref="Default"/> rather than an empty title.</summary>
    internal static string Normalize(string? text)
    {
        var printable = Printable(text);
        return printable.Length == 0 ? Default
            : printable.Length <= MaxLength ? printable
            : Cut(printable);
    }

    private static string Printable(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var builder = new System.Text.StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (char.IsControl(character))
                continue;

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    // A cut at the last space keeps the title readable; a single long word has no boundary to use,
    // so it is cut where the bound falls rather than dropped entirely.
    private static string Cut(string printable)
    {
        var head = printable[..MaxLength];
        var boundary = head.LastIndexOf(' ');
        return boundary > 0 ? head[..boundary] : head;
    }
}
