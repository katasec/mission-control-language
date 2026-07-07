using System.Security.Claims;

namespace ForgeUI.Services;

/// <summary>
/// Claim contract for federated identity (38.4). A stable <c>(issuer, subject)</c> is the
/// person's identity key. Issuer is stamped explicitly per scheme (dev / OIDC) so provisioning
/// never depends on claim-mapping quirks across providers — keeping the exit from any one IdP.
/// </summary>
public static class ForgeClaims
{
    /// <summary>Explicit issuer claim we stamp at sign-in (scheme-agnostic).</summary>
    public const string Issuer = "forge_iss";

    public static (string Issuer, string Subject)? TryGetIdentity(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        var issuer = user.FindFirst(Issuer)?.Value;
        var subject = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;

        return string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(subject)
            ? null
            : (issuer, subject);
    }

    public static string? Email(ClaimsPrincipal user)
        => user.FindFirst(ClaimTypes.Email)?.Value ?? user.FindFirst("email")?.Value;

    public static string DisplayName(ClaimsPrincipal user, string fallback)
    {
        // Use the name claim only when it's meaningful. Entra's Email-OTP flow collects no name
        // and defaults the `name` claim to the literal "unknown", so treat that (and blanks) as
        // absent and fall back to the email's local part (writeameer@gmail.com -> "Writeameer").
        var name = user.FindFirst(ClaimTypes.Name)?.Value ?? user.FindFirst("name")?.Value;
        if (!string.IsNullOrWhiteSpace(name) && !name.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            return name;

        var email = Email(user);
        if (!string.IsNullOrWhiteSpace(email))
        {
            var local = email.Split('@')[0];
            if (!string.IsNullOrWhiteSpace(local))
                return char.ToUpperInvariant(local[0]) + local[1..];
            return email;
        }

        return fallback;
    }
}
