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

    /// <summary>Role within this room (38.4). Gates provisioner-only actions.</summary>
    public MembershipRole Role { get; set; } = MembershipRole.Consumer;

    public DateTimeOffset JoinedAt { get; set; }
}
