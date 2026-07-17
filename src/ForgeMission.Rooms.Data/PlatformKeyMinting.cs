using System.Security.Cryptography;
using System.Text;

namespace ForgeMission.Rooms.Data;

/// <summary>
/// Format, mint, and verify platform keys (42.5). Shared by the issuer (ForgeUI, ①) and the
/// request-path resolver (runner lookup lib, ③) so both agree on the wire format and the hash.
///
/// Wire format: <c>fg_live_&lt;keyId&gt;_&lt;secret&gt;</c> — a support-friendly prefix, a lookup
/// <c>keyId</c>, and a random secret. Both segments are lowercase hex (no underscores), so the
/// single <c>'_'</c> after the keyId is an unambiguous delimiter. Only a <b>keyed hash</b> of the
/// secret is stored (HMAC-SHA256 under a shared server key); the plaintext key is shown to the user
/// exactly once at issuance and never persisted.
/// </summary>
public static class PlatformKeyMinting
{
    public const string Prefix = "fg_live_";

    public sealed record Minted(string KeyId, string Token, string SecretHash);

    /// <summary>Mint a fresh key. Returns the public token (return to the user once) plus the
    /// <see cref="Minted.KeyId"/> and <see cref="Minted.SecretHash"/> to persist.</summary>
    public static Minted Mint(string hmacKey)
    {
        var keyId  = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(12)); // 24 hex chars
        var secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)); // 64 hex chars
        var token  = $"{Prefix}{keyId}_{secret}";
        return new Minted(keyId, token, Hash(secret, hmacKey));
    }

    /// <summary>Split a presented token into its lookup id and secret, or null if malformed.</summary>
    public static (string KeyId, string Secret)? TryParse(string? token)
    {
        if (string.IsNullOrEmpty(token) || !token.StartsWith(Prefix, StringComparison.Ordinal))
            return null;

        var rest = token[Prefix.Length..];
        var sep = rest.IndexOf('_');
        if (sep <= 0 || sep >= rest.Length - 1)
            return null;

        return (rest[..sep], rest[(sep + 1)..]);
    }

    /// <summary>Keyed hash of a secret (HMAC-SHA256 under the shared server key), lowercase hex.</summary>
    public static string Hash(string secret, string hmacKey)
    {
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(hmacKey), Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexStringLower(mac);
    }

    /// <summary>Constant-time check that a presented secret matches a stored hash.</summary>
    public static bool Verify(string secret, string hmacKey, string expectedHash) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Hash(secret, hmacKey)),
            Encoding.UTF8.GetBytes(expectedHash));
}
