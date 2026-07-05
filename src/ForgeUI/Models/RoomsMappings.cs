using ForgeMission.Rooms;

namespace ForgeUI.Models;

/// <summary>Manual entity → DTO mapping. No AutoMapper (by design — see phase-38.1).</summary>
public static class RoomsMappings
{
    public static RoomMessageDto ToDto(this Message message, string senderName) => new(
        message.Id,
        message.RoomId,
        message.SenderId,
        senderName,
        message.SenderKind == MemberKind.Agent ? "agent" : "human",
        message.Payload.Text,
        message.ReplyTo,
        message.CreatedAt);
}
