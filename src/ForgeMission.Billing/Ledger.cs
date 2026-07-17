namespace ForgeMission.Billing;

/// <summary>What a <see cref="LedgerEntry"/> represents. F&amp;F (39.2) uses <see cref="Grant"/>
/// (comped starting credit) and <see cref="Debit"/> (a run's actual cost). <see cref="Topup"/> is
/// the Stripe/real-money credit added in 39.6 — same ledger, different origin.</summary>
public enum LedgerEntryKind
{
    Grant, // comped credit (F&F starting balance / coupon)
    Debit, // cost of a run, charged after settlement
    Topup, // real money added (Stripe) — 39.6
}

/// <summary>
/// One append-only movement on a member's balance, denominated in <b>integer micro-USD</b>
/// (millionths of a dollar; exact, no float drift, 1:1 to real money for Stripe later). A member's
/// balance is <c>SUM(AmountMicroUsd)</c> — positive entries credit, negative entries debit. Debits
/// carry the run's cost breakdown (tokens / compute-seconds / model / mission) so per-user cost is
/// observable and prices can be set from real data (the point of metering during F&amp;F).
/// </summary>
public sealed class LedgerEntry
{
    public Guid            Id             { get; set; }
    public Guid            MemberId       { get; set; }
    /// <summary>Signed amount in micro-USD: positive credits, negative debits.</summary>
    public long            AmountMicroUsd { get; set; }
    public LedgerEntryKind Kind           { get; set; }
    public string?         Description    { get; set; }

    // Debit cost breakdown (null for grants/topups) — the raw signal for pricing/telemetry.
    public string?         MissionRef     { get; set; }
    public string?         Model          { get; set; }
    public long?           InputTokens    { get; set; }
    public long?           OutputTokens   { get; set; }
    public double?         ComputeSeconds { get; set; }

    public DateTimeOffset  CreatedAt      { get; set; }
}
