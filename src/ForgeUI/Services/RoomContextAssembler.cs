using System.Text;
using ForgeMission.Rooms.Data;

namespace ForgeUI.Services;

/// <summary>
/// Assembles the room-scoped context an agent receives (parent Q1): the @-prompt plus a
/// bounded recent window (last N messages), <b>room-only</b> — never cross-room. If the
/// triggering message was a reply, the referenced message is prioritised ("the above").
/// All senders in the window are visible (human + other agents). The room is the
/// confidentiality boundary.
///
/// The window is composed into the mission's single input; the @-prompt is always the
/// explicit question so a bounded transcript never displaces it.
/// </summary>
public sealed class RoomContextAssembler(IReadStore reads)
{
    /// <summary>Recent-window size (N). Configurable knob (Q1); a token cap can layer on later.</summary>
    public int WindowSize { get; init; } = 10;

    public async Task<string> BuildGoalAsync(
        Guid roomId,
        string prompt,
        Guid? replyTo,
        Guid triggerMessageId,
        IReadOnlyDictionary<Guid, string> memberNames,
        CancellationToken ct = default)
    {
        // +1 so dropping the just-persisted trigger still leaves a full window.
        var recent = await reads.GetRecentMessagesAsync(roomId, WindowSize + 1, ct: ct);
        var prior = recent.Where(m => m.Id != triggerMessageId).ToList();

        var sb = new StringBuilder();

        if (replyTo is { } target)
        {
            var replied = prior.FirstOrDefault(m => m.Id == target);
            if (replied is not null)
                sb.AppendLine($"In reply to {Name(memberNames, replied.SenderId)}: \"{replied.Payload.Text}\"")
                  .AppendLine();
        }

        if (prior.Count > 0)
        {
            sb.AppendLine("Recent conversation in this room (for context):");
            foreach (var m in prior)
                sb.AppendLine($"  {Name(memberNames, m.SenderId)}: {m.Payload.Text}");
            sb.AppendLine();
        }

        sb.Append(prompt);
        return sb.ToString();
    }

    private static string Name(IReadOnlyDictionary<Guid, string> names, Guid id)
        => names.GetValueOrDefault(id, "unknown");
}
