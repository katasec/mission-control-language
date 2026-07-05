namespace ForgeMission.Rooms;

/// <summary>
/// A room invite link (38.4). The primary on-ramp (parent §7): tap link → sign in → auto-join
/// with the granted <see cref="Role"/>. The opaque <see cref="Token"/> is the shareable secret;
/// possession + a valid sign-in is what grants membership, so it is unguessable and can expire.
/// </summary>
public sealed class RoomInvite
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }

    /// <summary>Opaque, unguessable link token (URL-safe).</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Role granted to whoever accepts the invite.</summary>
    public MembershipRole Role { get; set; } = MembershipRole.Consumer;

    /// <summary>The provisioner who created it (attribution).</summary>
    public Guid CreatedBy { get; set; }

    /// <summary>Null = never expires.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public bool IsExpired(DateTimeOffset now) => ExpiresAt is { } e && e <= now;
}
