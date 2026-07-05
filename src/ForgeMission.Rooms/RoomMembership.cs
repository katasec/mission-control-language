namespace ForgeMission.Rooms;

/// <summary>
/// The confidentiality boundary (tenet 3). A member sees a room's messages iff a
/// membership row exists — enforced by the DB (FKs + UNIQUE (room_id, member_id)),
/// not by app code.
/// </summary>
public sealed class RoomMembership
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public Guid MemberId { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
}
