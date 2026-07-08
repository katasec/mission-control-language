using Microsoft.EntityFrameworkCore;

namespace ForgeMission.Rooms.Data;

/// <summary>
/// Persistence for the per-user balance ledger (39.2). Balance is <c>SUM(amount_micro_usd)</c> —
/// no separate cached-balance row, so it is always consistent (append-only entries can't diverge
/// from a materialized total). Fine at F&amp;F scale; a cached balance is a later optimisation.
/// </summary>
public interface ILedgerStore
{
    /// <summary>Current balance in micro-USD (0 if the member has no entries).</summary>
    Task<long> GetBalanceMicroUsdAsync(Guid memberId, CancellationToken ct = default);

    /// <summary>Append an entry (credit if positive, debit if negative).</summary>
    Task AppendAsync(LedgerEntry entry, CancellationToken ct = default);

    /// <summary>True if the member already has an entry of this kind — used to keep the one-time
    /// starting grant idempotent.</summary>
    Task<bool> HasEntryOfKindAsync(Guid memberId, LedgerEntryKind kind, CancellationToken ct = default);
}

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
