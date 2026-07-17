using ForgeMission.Billing;
using Microsoft.EntityFrameworkCore;

namespace ForgeMission.Rooms.Data;

/// <summary>
/// EF/Postgres implementation of <see cref="ILedgerStore"/> over <c>rooms_db</c> — the ForgeUI host's
/// ledger. (42.6 adds a raw-Npgsql sibling on ForgeAPI over <c>authbilling_db</c>; both satisfy the
/// same abstraction in <c>ForgeMission.Billing</c>.) Balance is <c>SUM(amount_micro_usd)</c>.
/// </summary>
public sealed class LedgerStore(IDbContextFactory<RoomsDbContext> factory) : ILedgerStore
{
    public async Task<long> GetBalanceMicroUsdAsync(Guid memberId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        // SumAsync over an empty set throws for non-nullable; project to long? and coalesce.
        return await db.LedgerEntries
            .Where(e => e.MemberId == memberId)
            .SumAsync(e => (long?)e.AmountMicroUsd, ct) ?? 0L;
    }

    public async Task AppendAsync(LedgerEntry entry, CancellationToken ct = default)
    {
        if (entry.Id == Guid.Empty) entry.Id = Guid.NewGuid();
        if (entry.CreatedAt == default) entry.CreatedAt = DateTimeOffset.UtcNow;
        await using var db = await factory.CreateDbContextAsync(ct);
        db.LedgerEntries.Add(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> HasEntryOfKindAsync(Guid memberId, LedgerEntryKind kind, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.LedgerEntries.AnyAsync(e => e.MemberId == memberId && e.Kind == kind, ct);
    }
}
