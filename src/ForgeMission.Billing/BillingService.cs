using ForgeMission.Runner.Contracts;
using Microsoft.Extensions.Logging;

namespace ForgeMission.Billing;

/// <summary>
/// The orchestrator's billing seam (39.2): grant credits, check balance before a run, and debit the
/// actual cost after it. Prepaid substrate — <b>balance = cap = meter</b>: a run is allowed while the
/// balance is positive and stops when it hits zero. Settlement is <b>debit-after-run</b> (no pre-auth
/// holds); near-empty overshoot is bounded by the runner's per-run timeout × max loop calls. F&amp;F
/// differ only by granted credits on this same ledger — not a separate code path.
/// </summary>
public sealed class BillingService(
    ILedgerStore ledger,
    BillingOptions options,
    ILogger<BillingService> logger)
{
    private long StartingCreditMicroUsd => options.StartingCreditMicroUsd;

    public Task<long> GetBalanceMicroUsdAsync(Guid memberId, CancellationToken ct = default) =>
        ledger.GetBalanceMicroUsdAsync(memberId, ct);

    /// <summary>A run is allowed while the balance is strictly positive (stop-at-zero = the cap).</summary>
    public async Task<bool> HasCreditAsync(Guid memberId, CancellationToken ct = default) =>
        await ledger.GetBalanceMicroUsdAsync(memberId, ct) > 0;

    /// <summary>Grant the one-time F&amp;F starting credit. Idempotent — a no-op if the member already
    /// has any Grant entry, so it is safe to call on every provisioning path.</summary>
    public async Task GrantStartingCreditAsync(Guid memberId, CancellationToken ct = default)
    {
        if (StartingCreditMicroUsd <= 0) return;
        if (await ledger.HasEntryOfKindAsync(memberId, LedgerEntryKind.Grant, ct)) return;

        await ledger.AppendAsync(new LedgerEntry
        {
            MemberId       = memberId,
            AmountMicroUsd = StartingCreditMicroUsd,
            Kind           = LedgerEntryKind.Grant,
            Description    = "F&F starting credit",
        }, ct);
        logger.LogInformation(
            "Granted starting credit {MicroUsd}µ$ to member {MemberId}", StartingCreditMicroUsd, memberId);
    }

    /// <summary>Debit a completed run's actual cost. Returns the debited amount (micro-USD).
    /// <paramref name="clientToken"/> is the M7 idempotency key (42.6 task 5a) — when present and a
    /// prior entry with the same token already exists, that entry's amount is returned instead of
    /// appending a second debit (buses/HTTP retries are at-least-once). Null preserves the original
    /// always-append behaviour for callers that don't carry a client token (e.g. Rooms).</summary>
    public async Task<long> SettleRunAsync(
        Guid memberId, string missionRef, RunUsage usage, CancellationToken ct = default,
        string? clientToken = null)
    {
        if (clientToken is { Length: > 0 })
        {
            var existing = await ledger.FindByClientTokenAsync(clientToken, ct);
            if (existing is not null)
            {
                logger.LogInformation(
                    "SettleRunAsync retried with known ClientToken {ClientToken} — returning prior debit {Cost}µ$, not double-charging.",
                    clientToken, -existing.AmountMicroUsd);
                return -existing.AmountMicroUsd;
            }
        }

        if (usage.Model is { Length: > 0 } && !CostMeter.IsKnownModel(usage.Model))
            logger.LogWarning(
                "No pricing rate for model '{Model}' — charged at fallback rate; add it to CostMeter.",
                usage.Model);

        var cost = CostMeter.PriceMicroUsd(usage);
        if (cost <= 0) return 0;

        await ledger.AppendAsync(new LedgerEntry
        {
            MemberId       = memberId,
            AmountMicroUsd = -cost,
            Kind           = LedgerEntryKind.Debit,
            Description     = $"Run {missionRef}",
            MissionRef     = missionRef,
            Model          = usage.Model,
            InputTokens    = usage.InputTokens,
            OutputTokens   = usage.OutputTokens,
            ComputeSeconds = usage.ComputeSeconds,
            ClientToken    = clientToken,
        }, ct);

        logger.LogInformation(
            "Debited {Cost}µ$ from member {MemberId} — {Mission} {In}+{Out} tok / {Secs:F2}s / {Model}",
            cost, memberId, missionRef, usage.InputTokens, usage.OutputTokens, usage.ComputeSeconds, usage.Model);
        return cost;
    }
}
