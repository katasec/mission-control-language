namespace ForgeMission.Rooms;

/// <summary>
/// Fluid message payload stored as jsonb. In 38.1 this is human text only; agent
/// messages (38.2/38.3) extend it with trace, trust signal, and artifact
/// *references* (bytes go to blob storage, never jsonb). Large binary never
/// belongs here.
/// </summary>
public sealed class MessagePayload
{
    /// <summary>Payload schema version — bump when the shape changes.</summary>
    public int V { get; set; } = 1;

    /// <summary>Discriminator: "human" | "agent" (mirrors <see cref="Message.Kind"/>).</summary>
    public string Kind { get; set; } = MessagePayloadKinds.Human;

    public string? Text { get; set; }

    /// <summary>
    /// Agent-only envelope: null for human messages. Produced in 38.2 (this phase stores it);
    /// the badge/trace it carries is *rendered* in 38.3. Plain POCOs so the domain stays
    /// dependency-free — the host maps Core's <c>StepEnvelope</c>/<c>TrustSignal</c> into these.
    /// </summary>
    public AgentMeta? Agent { get; set; }
}

/// <summary>Trust verdict + step trace for an agent message (jsonb, versioned via parent).</summary>
public sealed class AgentMeta
{
    /// <summary>The addressed handle, e.g. "@forge/hallucination-guard".</summary>
    public string Handle { get; set; } = string.Empty;

    /// <summary>Trust verdict. Stays in jsonb through 38.3 (see storage model); the first
    /// cross-message query is what promotes it to a generated column.</summary>
    public bool Verified { get; set; }

    public int StepCount { get; set; }
    public int RetryCount { get; set; }

    public List<AgentStep> Trace { get; set; } = [];
}

/// <summary>One expert step in the agent's run (for 38.3 trace rendering).</summary>
public sealed class AgentStep
{
    public string ExpertName { get; set; } = string.Empty;

    /// <summary>"pass" | "fail" (mirrors the step envelope status).</summary>
    public string Status { get; set; } = "pass";

    public string? Text { get; set; }

    /// <summary>Why a step failed (the judge's onFail feedback) — drives the retry line.</summary>
    public string? Reason { get; set; }

    public int Attempt { get; set; }
}

public static class MessagePayloadKinds
{
    public const string Human = "human";
    public const string Agent = "agent";
}
