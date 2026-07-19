namespace ForgeMission.Billing;

/// <summary>
/// Persistence for the per-user balance ledger (39.2). Balance is <c>SUM(amount_micro_usd)</c> —
/// no separate cached-balance row, so it is always consistent (append-only entries can't diverge
/// from a materialized total). Fine at F&amp;F scale; a cached balance is a later optimisation.
///
/// <para>The abstraction is the seam that lets the same <see cref="BillingService"/> back two hosts:
/// ForgeUI's EF <c>LedgerStore</c> over <c>rooms_db</c>, and (42.6) ForgeAPI's raw-Npgsql store over
/// the AOT-clean <c>authbilling_db</c>.</para>
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

    /// <summary>The entry previously appended with this <see cref="LedgerEntry.ClientToken"/>, if
    /// any — M7 idempotency lookup so a retried run can return the prior debit instead of a new one.</summary>
    Task<LedgerEntry?> FindByClientTokenAsync(string clientToken, CancellationToken ct = default);
}
