namespace ForgeMission.Rooms;

/// <summary>
/// A participant that can belong to rooms and send messages.
/// Agents are members, not tools — the two kinds share one identity space
/// so a message sender is always a Member regardless of kind.
/// </summary>
public sealed class Member
{
    public Guid Id { get; set; }
    public MemberKind Kind { get; set; }
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Federated identity (38.4). The stable IdP subject (<c>sub</c>) and issuer (<c>iss</c>)
    /// are the person's identity key — unique together — so the same human across sign-ins maps
    /// to one member. Null for agents (a mission has no login) and for legacy rows.
    /// </summary>
    public string? Subject { get; set; }
    public string? Issuer { get; set; }

    /// <summary>Verified email from the IdP (needed for invites + trust). Null for agents.</summary>
    public string? Email { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
