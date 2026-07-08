using System.Security.Claims;
using ForgeMission.Rooms;
using ForgeMission.Rooms.Data;

namespace ForgeUI.Services;

/// <summary>
/// Just-in-time provisioning (38.4): maps an authenticated principal to a domain Member,
/// keyed by the stable (issuer, subject). The IdP proves *who* the person is; the Member is
/// *our* record of them. Authorization (room membership) is separate and DB-enforced.
/// </summary>
public sealed class MemberProvisioningService(
    IReadStore reads, IWriteStore writes, BillingService billing, ILogger<MemberProvisioningService> logger)
{
    /// <summary>Resolve (and provision on first sight) the Member for a principal, or null if unauthenticated.</summary>
    public async Task<Member?> ResolveAsync(ClaimsPrincipal? user, CancellationToken ct = default)
    {
        if (ForgeClaims.TryGetIdentity(user) is not { } id)
            return null;

        return await FindOrCreateAsync(id.Issuer, id.Subject, ForgeClaims.Email(user!), ForgeClaims.DisplayName(user!, id.Subject), ct);
    }

    public async Task<Member> FindOrCreateAsync(string issuer, string subject, string? email, string displayName, CancellationToken ct = default)
    {
        var member = await ResolveOrCreateAsync(issuer, subject, email, displayName, ct);

        // Auto-grant the one-time F&F starting credit (39.2). Idempotent (no-op if already granted),
        // and run on EVERY resolve — not just first create — so members that pre-date 39.2 self-heal
        // their starting balance on next sign-in (same pattern as the starter-room self-heal).
        // Isolated so a billing/ledger hiccup never blocks sign-in.
        try
        {
            await billing.GrantStartingCreditAsync(member.Id, ct);
        }
        catch (Exception grantEx)
        {
            logger.LogError(grantEx, "Failed to grant starting credit to member {MemberId}", member.Id);
        }

        return member;
    }

    private async Task<Member> ResolveOrCreateAsync(string issuer, string subject, string? email, string displayName, CancellationToken ct)
    {
        var existing = await reads.GetMemberBySubjectAsync(issuer, subject, ct);
        if (existing is not null)
        {
            if (existing.DisplayName != displayName || existing.Email != email)
                await writes.UpdateMemberProfileAsync(existing.Id, displayName, email, ct);
            return existing;
        }

        try
        {
            var created = await writes.AddMemberAsync(new Member
            {
                Kind = MemberKind.Human,
                DisplayName = displayName,
                Issuer = issuer,
                Subject = subject,
                Email = email,
            }, ct);
            logger.LogInformation("Provisioned member {MemberId} for {Issuer}/{Subject}", created.Id, issuer, subject);
            return created;
        }
        catch (Exception ex)
        {
            // Concurrent first-login racing the unique (issuer, subject) index — re-read the winner.
            logger.LogWarning(ex, "Provisioning race for {Issuer}/{Subject}; re-reading", issuer, subject);
            var winner = await reads.GetMemberBySubjectAsync(issuer, subject, ct);
            if (winner is not null)
                return winner;
            throw;
        }
    }
}
