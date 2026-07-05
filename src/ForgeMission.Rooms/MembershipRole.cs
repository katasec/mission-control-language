namespace ForgeMission.Rooms;

/// <summary>
/// A member's role *within a room* (38.4). Provisioner vs consumer is a role difference, not an
/// auth difference (parent §7): the provisioner authors on behalf of the group (invites others,
/// configures/adds agents in 38.5); consumers can only talk.
/// </summary>
public enum MembershipRole
{
    Consumer,
    Provisioner
}
