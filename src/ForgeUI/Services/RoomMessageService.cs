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
        var agents = (await reads.GetRoomMembersAsync(roomId, ct))
            .Where(m => m.Kind == MemberKind.Agent)
            .ToList();
        if (agents.Count == 0)
            return;

        if (MentionParser.Detect(humanMessage.Payload.Text ?? string.Empty, agents.Select(a => a.DisplayName)) is not { } hit)
            return;

        var agent = agents.First(a => string.Equals(a.DisplayName, hit.Handle, StringComparison.OrdinalIgnoreCase));
        invoker.Invoke(roomId, agent, hit.Handle, hit.Prompt, replyTo: humanMessage.ReplyTo, triggerMessageId: humanMessage.Id);
    }
}
