using ForgeMission.Rooms;

namespace ForgeMission.Rooms.Tests;

/// <summary>
/// The no-false-green guarantee (38.3 task 5): a ✓ Verified badge must be impossible to
/// produce for an answer that was not actually verified.
/// </summary>
public sealed class TrustIntegrityTests
{
    private static AgentMeta Meta(bool verified, params (string status, string expert)[] steps) => new()
    {
        Handle = "@forge/hallucination-guard",
        Verified = verified,
        StepCount = steps.Length,
        Trace = steps.Select(s => new AgentStep { ExpertName = s.expert, Status = s.status }).ToList(),
    };

    [Fact]
    public void Green_only_when_verdict_passes_and_trace_ends_on_a_pass()
    {
        var meta = Meta(verified: true, ("pass", "Answerer"), ("fail", "Verifier"),
            ("pass", "Answerer"), ("pass", "Verifier"));

        Assert.True(TrustIntegrity.IsVerified(meta));
    }

    [Fact]
    public void Failed_run_can_never_produce_green()
    {
        var meta = Meta(verified: false, ("pass", "Answerer"), ("fail", "Verifier"));

        Assert.False(TrustIntegrity.IsVerified(meta));
    }

    [Fact]
    public void Inconsistent_verdict_cannot_produce_green_when_trace_ends_on_a_fail()
    {
        // Defence in depth: even a corrupt verdict=true on a run whose trace terminates in a
        // failure must render unverified — no false green.
        var meta = Meta(verified: true, ("pass", "Answerer"), ("fail", "Verifier"));

        Assert.False(TrustIntegrity.IsVerified(meta));
    }

    [Fact]
    public void Verdict_true_with_empty_trace_is_not_green()
    {
        var meta = Meta(verified: true);

        Assert.False(TrustIntegrity.IsVerified(meta));
    }

    [Fact]
    public void Null_meta_is_not_green()
    {
        Assert.False(TrustIntegrity.IsVerified(null));
    }
}
