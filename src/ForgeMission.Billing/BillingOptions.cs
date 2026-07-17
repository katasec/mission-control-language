namespace ForgeMission.Billing;

/// <summary>
/// Host-supplied billing policy. Kept as a plain injected record rather than read from
/// <c>IConfiguration</c> inside the lib so <see cref="BillingService"/> stays free of the config
/// binder's reflection (the lib is an AOT target — 42.6). The host binds it from config once at
/// startup and registers it as a singleton.
/// </summary>
public sealed record BillingOptions
{
    /// <summary>One-time comped starting balance for a new user, in micro-USD. Default $5.</summary>
    public long StartingCreditMicroUsd { get; init; } = 5_000_000L;
}
