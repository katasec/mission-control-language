using System.Security.Claims;
using ForgeMission.Rooms.Data;
using ForgeUI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace ForgeUI;

/// <summary>
/// Platform-key issuance (42.5 ①). `forge login` gets an Entra access token for the Rooms
/// <c>cli.login</c> scope, then POSTs it here; we validate that bearer, resolve (provision + grant)
/// the member, mint a platform key, store only its hash, and return the plaintext key once.
///
/// This is the ONLY caller-facing endpoint authenticated by an Entra access token — the interactive
/// web app uses the cookie/OIDC scheme, and later request-path endpoints (<c>/me</c>, runs) are
/// authenticated by the platform key itself (③).
/// </summary>
public static class PlatformKeyEndpoints
{
    public const string BearerScheme = "PlatformKeyBearer";

    private const string HmacKeyDevDefault = "dev-platform-key-hmac-do-not-use-in-prod";

    // --- registration -------------------------------------------------------------------------

    /// <summary>Add the JWT-bearer scheme that validates CLI access tokens. Call only when OIDC is
    /// configured (real Entra); local dev has no token issuer.</summary>
    public static void AddPlatformKeyBearer(this AuthenticationBuilder auth, IConfiguration config)
    {
        var authority = config["Oidc:Authority"];
        // The CLI requests `api://<roomsAppId>/cli.login`. Entra v2 access tokens carry `aud` as the
        // bare app-id GUID (verified live), but older/other configs use the `api://` App ID URI — accept
        // both so the audience check is robust to either form.
        var roomsAppId = config["PlatformKeys:RoomsAppId"] ?? "4f8a95d6-2d41-416c-a1b9-9177ddec1227";
        // CIAM quirk (verified live): the friendly-host authority (`forgeids.ciamlogin.com`) issues
        // tokens whose `iss` uses the tenant-GUID host (`<tenantId>.ciamlogin.com`), so the metadata
        // issuer doesn't match. Accept that form explicitly. Pull the tenant id out of the authority
        // by finding the GUID segment — robust whether or not the authority ends in `/v2.0` (prod does,
        // local dev doesn't).
        var tenantId = (authority ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault(p => Guid.TryParse(p, out _)) ?? "";

        auth.AddJwtBearer(BearerScheme, options =>
        {
            options.Authority = authority;
            options.TokenValidationParameters.ValidAudiences = [roomsAppId, $"api://{roomsAppId}"];
            options.TokenValidationParameters.ValidIssuers =
            [
                $"https://{tenantId}.ciamlogin.com/{tenantId}/v2.0",
            ];
            options.MapInboundClaims = false; // keep raw `oid` / `scp` claim names
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = ctx =>
                {
                    // Stamp the same scheme-level issuer the OIDC path stamps, so provisioning keys
                    // the member identically whether they arrived by cookie or by CLI bearer.
                    if (ctx.Principal?.Identity is ClaimsIdentity id && id.FindFirst(ForgeClaims.Issuer) is null)
                        id.AddClaim(new Claim(ForgeClaims.Issuer, "entra-external-id"));
                    return Task.CompletedTask;
                },
            };
        });
    }

    public static void MapPlatformKeys(this WebApplication app)
    {
        // ① Issuance — authenticated by the CLI's Entra access token.
        app.MapPost("/platform/keys", IssueAsync)
            .RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute
            {
                AuthenticationSchemes = BearerScheme,
            });

        // ④ Account query — authenticated by the platform key itself (resolved via ③), not the
        // Entra bearer: `whoami` runs later carrying only the stored key.
        app.MapGet("/me", MeAsync);
    }

    // --- handler ------------------------------------------------------------------------------

    private static async Task<IResult> IssueAsync(
        ClaimsPrincipal principal,
        MemberProvisioningService provisioning,
        IPlatformKeyStore keys,
        BillingService billing,
        IConfiguration config,
        ILoggerFactory loggers,
        CancellationToken ct)
    {
        if (!HasScope(principal, "cli.login"))
            return Results.Forbid();

        // Provisions the member on first sight and grants the one-time credit (both idempotent).
        var member = await provisioning.ResolveAsync(principal, ct);
        if (member is null)
            return Results.Unauthorized();

        var minted = PlatformKeyMinting.Mint(HmacKey(config));
        await keys.SaveAsync(new PlatformKey
        {
            KeyId      = minted.KeyId,
            SecretHash = minted.SecretHash,
            MemberId   = member.Id,
        }, ct);

        var balance = await billing.GetBalanceMicroUsdAsync(member.Id, ct);
        loggers.CreateLogger(typeof(PlatformKeyEndpoints)).LogInformation(
            "Issued platform key {KeyId} to member {MemberId}", minted.KeyId, member.Id);

        // The plaintext token is returned exactly once — never stored, never logged.
        return Results.Ok(new IssueResponse(minted.Token, member.Email, balance));
    }

    // --- /me (④) ------------------------------------------------------------------------------

    private static async Task<IResult> MeAsync(
        HttpContext http,
        IPlatformKeyResolver resolver,
        IReadStore reads,
        CancellationToken ct)
    {
        var token = BearerToken(http);
        var ctx = token is null ? null : await resolver.ResolveAsync(token, ct);
        if (ctx is null)
            return Results.Unauthorized();

        var member = await reads.GetMemberAsync(ctx.MemberId, ct);
        return Results.Ok(new MeResponse(member?.Email, member?.DisplayName, ctx.BalanceMicroUsd));
    }

    private static string? BearerToken(HttpContext http)
    {
        var header = http.Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : null;
    }

    // --- helpers ------------------------------------------------------------------------------

    private static bool HasScope(ClaimsPrincipal user, string scope)
    {
        // Entra delegated scopes arrive in `scp` as a space-delimited string.
        var scp = user.FindFirst("scp")?.Value;
        return scp is not null && scp.Split(' ').Contains(scope);
    }

    /// <summary>Shared server key for the platform-key HMAC — issuer and resolver must agree.</summary>
    internal static string HmacKey(IConfiguration config) =>
        config["PlatformKeys:HmacKey"] ?? HmacKeyDevDefault;

    private sealed record IssueResponse(string Key, string? Email, long BalanceMicroUsd);
    private sealed record MeResponse(string? Email, string? DisplayName, long BalanceMicroUsd);
}
