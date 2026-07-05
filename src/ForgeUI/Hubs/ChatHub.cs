using ForgeMission.Rooms;
using ForgeMission.Rooms.Data;
using ForgeUI.Models;
using Microsoft.AspNetCore.SignalR;

namespace ForgeUI.Hubs;

/// <summary>
/// Realtime room delivery: one SignalR group per room, broadcasts scoped to that
/// group only. Speaks DTOs, never entities.
///
/// Dev-stub trust model (38.1): the client asserts its member id; every call is
/// checked against the memberships table, but the assertion itself is unverified
/// until real identity lands in 38.4.
/// </summary>
public sealed class ChatHub(IReadStore reads, IWriteStore writes) : Hub
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
    }

    private static string GroupName(Guid roomId) => $"room:{roomId}";
}
