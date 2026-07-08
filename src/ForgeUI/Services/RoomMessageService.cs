using System.Text.RegularExpressions;
using ForgeMission.Rooms;
using ForgeMission.Rooms.Data;
using ForgeUI.Models;

namespace ForgeUI.Services;

/// <summary>
/// The one membership-checked send path (38.4), shared by the Blazor client and the SignalR
/// hub. The sender is an already-authenticated Member (never a client-supplied id); this
/// re-checks room membership server-side, appends, fans out via <see cref="RoomBroadcaster"/>,
/// and runs the pull gate. Confidentiality is enforced here, once.
/// </summary>
public sealed class RoomMessageService(
    IReadStore reads, IWriteStore writes, RoomBroadcaster broadcaster, RoomAgentInvoker invoker)
{
    public async Task<bool> SendHumanMessageAsync(Guid roomId, Member sender, string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        if (sender.Kind != MemberKind.Human)
            throw new InvalidOperationException("Only humans send through this path.");
        if (await reads.GetMembershipAsync(roomId, sender.Id, ct) is null)
            throw new UnauthorizedAccessException("Not a member of this room.");

        var message = await writes.AppendMessageAsync(new Message
        {
            RoomId = roomId,
            SenderId = sender.Id,
            SenderKind = MemberKind.Human,
            Kind = MessageKind.Human,
            Payload = new MessagePayload { Kind = MessagePayloadKinds.Human, Text = text.Trim() },
        }, ct);

        await broadcaster.PublishMessageAsync(roomId, message.ToDto(sender.DisplayName));

        await TryInvokeAgentAsync(roomId, message, ct);
        return true;
    }

    // Pull gate (tenet 2): only a human message can address an agent member of the room.
    private async Task TryInvokeAgentAsync(Guid roomId, Message humanMessage, CancellationToken ct)
    {
        var members = await reads.GetRoomMembersAsync(roomId, ct);
        var agents = members.Where(m => m.Kind == MemberKind.Agent).ToList();
        if (agents.Count == 0)
            return;

        var text = humanMessage.Payload.Text ?? string.Empty;

        // Explicit @mention always wins.
        if (MentionParser.Detect(text, agents.Select(a => a.DisplayName)) is { } hit)
        {
            var mentioned = agents.First(a => string.Equals(a.DisplayName, hit.Handle, StringComparison.OrdinalIgnoreCase));
            invoker.Invoke(roomId, mentioned, hit.Handle, hit.Prompt, replyTo: humanMessage.ReplyTo, triggerMessageId: humanMessage.Id);
            return;
        }

        // "Room of two" auto-reply: when the room is exactly one human + one agent, the sole
        // agent answers every message with no @mention needed — a scoped exception to pull-only
        // that makes a 1:1 room feel like a normal chat. Group rooms still require addressing.
        //
        // But only when the sender did NOT explicitly address someone. An unmatched @mention
        // (e.g. "@openai …" in a room where @openai isn't a member) is a deliberate address to a
        // non-member — letting the sole agent answer it would falsely look like that agent replied
        // (the task-7 review bug). Suppress instead; the client nudges the user to add the agent.
        var humanCount = members.Count(m => m.Kind == MemberKind.Human);
        if (agents.Count == 1 && humanCount == 1 && !HasExplicitMention(text))
        {
            var sole = agents[0];
            invoker.Invoke(roomId, sole, sole.DisplayName, text.Trim(), replyTo: humanMessage.ReplyTo, triggerMessageId: humanMessage.Id);
        }
    }

    // A standalone "@handle" token — signals intent to address someone specific. Used only to
    // decide whether auto-reply should fire, never to resolve the target.
    private static readonly Regex MentionToken = new(@"(?<![\w@])@[A-Za-z][\w-]*", RegexOptions.Compiled);

    private static bool HasExplicitMention(string text) => MentionToken.IsMatch(text);
}
