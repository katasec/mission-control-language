namespace ForgeMission.Billing;

/// <summary>
/// A platform key (42.5): the opaque bearer token a signed-in user points a coding agent at the
/// hosted <c>/v1</c> endpoint with. The wire format is <c>fg_live_&lt;KeyId&gt;_&lt;secret&gt;</c> — a
/// support-friendly prefix, a lookup <see cref="KeyId"/>, and a random secret. We persist only a
/// <b>keyed hash of the secret</b> (<see cref="SecretHash"/>), never the plaintext, so a DB compromise
/// doesn't leak usable keys. Resolution: parse the KeyId from the token, load this row, hash the
/// presented secret, compare. A non-null <see cref="RevokedAt"/> means reject.
/// </summary>
public sealed class PlatformKey
{
    /// <summary>Lookup identifier embedded in the token (the <c>&lt;KeyId&gt;</c> segment). Unique.</summary>
    public string         KeyId      { get; set; } = "";

    /// <summary>Keyed hash of the secret segment — never the plaintext secret.</summary>
    public string         SecretHash { get; set; } = "";

    /// <summary>The member this key authenticates as; its ledger balance is what runs are metered against.</summary>
    public Guid           MemberId   { get; set; }

    public DateTimeOffset CreatedAt  { get; set; }

    /// <summary>Set when revoked; a revoked key is rejected on the request path (42.5 T6).</summary>
    public DateTimeOffset? RevokedAt { get; set; }
}
