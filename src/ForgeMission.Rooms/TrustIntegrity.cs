namespace ForgeMission.Rooms;

/// <summary>
/// The no-false-green guard (38.3 task 5). A ✓ Verified badge must be impossible to produce
/// for an answer that was not actually verified. This is the single source of truth for the
/// verified verdict — the render layer must derive green from here, never from a raw flag.
///
/// Defence in depth: even if an upstream bug set <see cref="AgentMeta.Verified"/> true on a
/// run whose trace terminates in a failure, this returns false. Green requires both the pass
/// verdict AND a trace that ends on a passing step.
/// </summary>
public static class TrustIntegrity
{
    public static bool IsVerified(AgentMeta? agent)
    {
        if (agent is null || !agent.Verified)
            return false;

        // A verified answer always has a trace, and its terminal step must have passed.
        if (agent.Trace.Count == 0)
            return false;

        return agent.Trace[^1].Status == "pass";
    }
}
