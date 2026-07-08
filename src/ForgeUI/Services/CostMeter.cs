using ForgeMission.Runner.Contracts;

namespace ForgeUI.Services;

/// <summary>
/// Prices a run's emitted signals (<c>tokens + compute-seconds</c>) into micro-USD (39.2). Decision:
/// <b>cost-recovery — true provider list price, no markup</b>. The meter records real cost so F&amp;F
/// yields the per-user data that sets public prices in 39.6; margin is a pricing-policy layer added
/// there, on top of this same meter. Rates are dated and table-driven — updating a price is one line.
/// </summary>
public static class CostMeter
{
    // micro-USD per token, from published provider list prices (dated 2026-07).
    // OpenAI: gpt-4o-mini $0.15/1M in, $0.60/1M out; gpt-4o $2.50/1M in, $10.00/1M out.
    //   ($0.15 / 1e6 tokens = 0.15 micro-USD/token, since $1 = 1e6 micro-USD.)
    // Claude/Grok models aren't loaded in prod yet (no keys) — add verified rates when they go live.
    private static readonly Dictionary<string, (double InPerToken, double OutPerToken)> ModelRates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-4o-mini"] = (0.15, 0.60),
            ["gpt-4o"]      = (2.50, 10.00),
        };

    // Fallback for an unrecognised model — priced at the most expensive known rate so an unknown
    // model is never *under*-charged (BillingService logs a warning to add the real rate).
    private static readonly (double InPerToken, double OutPerToken) FallbackRate = (2.50, 10.00);

    // ACA consumption compute cost for the runner's shape (0.5 vCPU + 1 GiB), 2026-07 uaenorth:
    // ~$0.000024/vCPU-s + ~$0.000003/GiB-s → 0.5*24 + 1*3 = 15 micro-USD/second.
    public const double ComputeMicroUsdPerSecond = 15.0;

    public static bool IsKnownModel(string? model) => model is not null && ModelRates.ContainsKey(model);

    /// <summary>Price a run in micro-USD, rounded up so rounding never under-charges.</summary>
    public static long PriceMicroUsd(RunUsage usage)
    {
        var (inRate, outRate) = usage.Model is { } m && ModelRates.TryGetValue(m, out var rate)
            ? rate
            : FallbackRate;

        var tokenCost   = usage.InputTokens * inRate + usage.OutputTokens * outRate;
        var computeCost = usage.ComputeSeconds * ComputeMicroUsdPerSecond;
        return (long)Math.Ceiling(tokenCost + computeCost);
    }
}
