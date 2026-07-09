using ForgeMission.Rooms;

namespace ForgeMission.Rooms.Data;

/// <summary>
/// A room member paired with the role they hold in that room — the join of a
/// <see cref="Member"/> and its <see cref="RoomMembership"/>. Used to render the members roster,
/// which needs both the identity (name/kind) and the role tag (provisioner vs consumer) in one read.
/// </summary>
public sealed record RoomMemberInfo(Member Member, MembershipRole Role);
