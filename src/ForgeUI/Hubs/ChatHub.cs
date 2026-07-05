using ForgeMission.Rooms;
using ForgeMission.Rooms.Data;
using ForgeUI.Models;
using ForgeUI.Services;
using Microsoft.AspNetCore.SignalR;
// MentionParser + Mention now live in ForgeMission.Rooms (pure domain, testable).

namespace ForgeUI.Hubs;

/// <summary>
/// Realtime room delivery: one SignalR group per room, broadcasts scoped to that
/// group only. Speaks DTOs, never entities.
///
/// Dev-stub trust model (38.1): the client asserts its member id; every call is
/// checked against the memberships table, but the assertion itself is unverified
/// until real identity lands in 38.4.
///
/// Pull gate (38.2): after a human message is persisted, if it @-addresses an agent
/// member of the room, the matching mission is invoked in the background. Non-addressed
/// messages are just chat (tenet 2 — pull, never push).
/// </summary>
public sealed class ChatHub(IReadStore reads, IWriteStore writes, RoomAgentInvoker invoker) : Hub
{
    public async Task JoinRoom(Guid roomId, Guid memberId)
    {
        if (!await reads.IsMemberAsync(roomId, memberId))
            throw new HubException("Not a member of this room.");
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(roomId));
    }

    public Task LeaveRoom(Guid roomId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(roomId));

    public async Task SendMessage(Guid roomId, Guid senderId, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        var sender = await reads.GetMemberAsync(senderId)
            ?? throw new HubException("Unknown sender.");
        if (!await reads.IsMemberAsync(roomId, senderId))
            throw new HubException("Not a member of this room.");

        var isAgent = sender.Kind == MemberKind.Agent;
        var message = await writes.AppendMessageAsync(new Message
        {
            RoomId = roomId,
            SenderId = sender.Id,
            SenderKind = sender.Kind,
            Kind = isAgent ? MessageKind.Agent : MessageKind.Human,
            Payload = new MessagePayload
            {
                Kind = isAgent ? MessagePayloadKinds.Agent : MessagePayloadKinds.Human,
                Text = text.Trim(),
            },
        });

        await Clients.Group(GroupName(roomId)).SendAsync("ReceiveMessage", message.ToDto(sender.DisplayName));

        // Pull gate: only a human message can address an agent (no cross-agent orchestration in v1).
        if (!isAgent)
            await TryInvokeAgentAsync(roomId, message);
    }

    private async Task TryInvokeAgentAsync(Guid roomId, Message humanMessage)
    {
        var agents = (await reads.GetRoomMembersAsync(roomId))
            .Where(m => m.Kind == MemberKind.Agent)
            .ToList();
        if (agents.Count == 0)
            return;

        var mention = MentionParser.Detect(humanMessage.Payload.Text ?? string.Empty, agents.Select(a => a.DisplayName));
        if (mention is not { } hit)
            return;

        var agent = agents.First(a => string.Equals(a.DisplayName, hit.Handle, StringComparison.OrdinalIgnoreCase));
        invoker.Invoke(roomId, agent, hit.Handle, hit.Prompt, replyTo: humanMessage.ReplyTo, triggerMessageId: humanMessage.Id);
    }

    public static string GroupName(Guid roomId) => $"room:{roomId}";
}
