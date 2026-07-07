namespace ForgeMission.Rooms;

/// <summary>
/// Where an agent is visible and addressable (38.5 task 1). An agent enters the directory by
/// being saved at a scope by a provisioner — that is the democratisation unit (one author,
/// many consumers).
/// </summary>
public enum AgentScope
{
    /// <summary>Only the owner can address it.</summary>
    Personal,

    /// <summary>Everyone in one room can address it.</summary>
    Room,

    /// <summary>Discoverable by everyone (built-ins and published agents).</summary>
    Shared,
}

/// <summary>
/// The per-agent identity seal (38.5 task 8a) — anti-impersonation, X-checkmark style: "this is
/// the official <c>@claude</c> / a verified publisher." Distinct from the per-response Verified
/// badge (38.3), which says a single <em>run</em> was verified. The two must stay separate: a
/// raw <c>@claude</c> may carry an <see cref="Official"/> seal while its individual answers get
/// no verified badge (unverified by design).
/// </summary>
public enum IdentitySeal
{
    /// <summary>Unclaimed handle / unknown publisher — no seal shown.</summary>
    None,

    /// <summary>A verified third-party publisher.</summary>
    Verified,

    /// <summary>Forge's own built-in agents (<c>@assistant</c>, <c>@guard</c>, <c>@claude</c>…).</summary>
    Official,
}

/// <summary>
/// A single entry in the Global Address List (38.5 task 1): a bare <c>@handle</c> bound to a
/// mission, carrying scope + owner + provenance (publisher + identity seal). Replaces the
/// hardcoded <c>@handle → mission</c> map from 38.2.
/// <para>
/// This is pure metadata — resolving a descriptor to a <em>runnable</em> mission needs the
/// loaded engine and is therefore the host's job (see <c>AgentRegistry</c> in ForgeUI). Keeping
/// the descriptor engine-free lets the directory model be tested in isolation and persisted
/// later (save-as-agent, 38.5 task 4) without dragging in provider types.
/// </para>
/// </summary>
public sealed record AgentDescriptor
{
    /// <summary>Bare, globally-unique, claimed handle — e.g. <c>@assistant</c> (38.5 task 6).</summary>
    public required string Handle { get; init; }

    /// <summary>One-line description shown in the <c>/agents</c> directory (38.5 task 9).</summary>
    public required string Description { get; init; }

    /// <summary>Provenance — who published this agent, shown in <c>/agents</c> (38.5 task 9).</summary>
    public required string Publisher { get; init; }

    /// <summary>Binding key to the loaded mission the host resolves this handle to.</summary>
    public required string MissionRef { get; init; }

    /// <summary>Visibility scope (38.5 task 1). Built-ins are <see cref="AgentScope.Shared"/>.</summary>
    public AgentScope Scope { get; init; } = AgentScope.Shared;

    /// <summary>Owning member; null for built-ins (a platform agent has no personal owner).</summary>
    public Guid? OwnerId { get; init; }

    /// <summary>Identity/publisher seal (38.5 task 8a). Not the per-response Verified badge.</summary>
    public IdentitySeal Seal { get; init; } = IdentitySeal.None;

    /// <summary>
    /// True for official handles reserved before custom/marketplace opens (38.5 task 6) — cheap
    /// insurance against impersonation while the namespace is first-come-first-served.
    /// </summary>
    public bool Reserved { get; init; }
}
