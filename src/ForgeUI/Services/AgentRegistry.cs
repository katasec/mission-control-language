using ForgeMission.Rooms;

namespace ForgeUI.Services;

/// <summary>
/// The Global Address List (38.5 task 1): a scoped <c>@handle → agent</c> directory that
/// replaces the hardcoded map from 38.2. Each entry pairs an <see cref="AgentDescriptor"/>
/// (handle + scope + owner + provenance) with the runnable <see cref="MissionEntry"/> it binds
/// to. Resolution lives host-side because binding to a mission needs the loaded engine.
/// <para>
/// Built-in agents are registered as <see cref="AgentScope.Shared"/> / <see cref="IdentitySeal.Official"/>
/// at startup. User-authored agents and persisted personal/room scopes arrive in later 38.5
/// tasks (save-as-agent, task 4); the descriptor model is already shaped for them.
/// </para>
/// </summary>
public sealed class AgentRegistry
{
    private readonly Dictionary<string, RegisteredAgent> _byHandle = new(StringComparer.OrdinalIgnoreCase);

    public AgentRegistry(MissionRegistry registry)
    {
        // @guard → the verify-loop mission (registry label "Forge").
        Register(registry, new AgentDescriptor
        {
            Handle          = "@guard",
            Description     = "Checks a claim for hallucinations with a deterministic verify loop.",
            Publisher       = "Forge",
            MissionRef      = "Forge",
            Scope           = AgentScope.Shared,
            Seal            = IdentitySeal.Official,
            Reserved        = true,
            VerifiesAnswers = true,
        });

        // @assistant → general answer verified by an LLM judge (registry label "Assistant").
        // The default agent dropped into every new user's starter room.
        Register(registry, new AgentDescriptor
        {
            Handle          = "@assistant",
            Description     = "General assistant whose answers are verified by an LLM judge.",
            Publisher       = "Forge",
            MissionRef      = "Assistant",
            Scope           = AgentScope.Shared,
            Seal            = IdentitySeal.Official,
            Reserved        = true,
            VerifiesAnswers = true,
        });

        // Raw-model passthrough agents (38.5 task 7): thin single-expert missions bound to a
        // provider. They carry the Official *identity* seal (they really are Forge's OpenAI/Claude
        // passthrough) but VerifiesAnswers = false — their answers are never green-checked, because
        // nothing verifies them. Raw beside verified in one room is the differentiation.
        // @openai reuses the "ChatGPT" (vanilla) mission; @claude binds the Anthropic mission and
        // only registers when ANTHROPIC_API_KEY is set (otherwise its mission isn't loaded).
        Register(registry, new AgentDescriptor
        {
            Handle          = "@openai",
            Description     = "Raw OpenAI model — a direct answer, not verified.",
            Publisher       = "Forge",
            MissionRef      = "ChatGPT",
            Scope           = AgentScope.Shared,
            Seal            = IdentitySeal.Official,
            Reserved        = true,
            VerifiesAnswers = false,
        });

        Register(registry, new AgentDescriptor
        {
            Handle          = "@claude",
            Description     = "Raw Claude model — a direct answer, not verified.",
            Publisher       = "Forge",
            MissionRef      = "Claude",
            Scope           = AgentScope.Shared,
            Seal            = IdentitySeal.Official,
            Reserved        = true,
            VerifiesAnswers = false,
        });

        Register(registry, new AgentDescriptor
        {
            Handle          = "@grok",
            Description     = "Raw Grok (xAI) model — a direct answer, not verified.",
            Publisher       = "Forge",
            MissionRef      = "Grok",
            Scope           = AgentScope.Shared,
            Seal            = IdentitySeal.Official,
            Reserved        = true,
            VerifiesAnswers = false,
        });
    }

    /// <summary>
    /// Bind a descriptor to its loaded mission. Skips silently when the mission isn't loaded
    /// (e.g. no <c>forge.toml</c> / empty API key) — matching 38.2's behaviour of simply not
    /// exposing an agent whose mission is unavailable.
    /// </summary>
    private void Register(MissionRegistry registry, AgentDescriptor descriptor)
    {
        var mission = registry.Missions.FirstOrDefault(m => m.Label == descriptor.MissionRef);
        if (mission is not null)
            _byHandle[descriptor.Handle] = new RegisteredAgent(descriptor, mission);
    }

    /// <summary>Resolve a handle to the runnable mission (the 38.2 invoke path).</summary>
    public bool TryResolve(string handle, out MissionEntry mission)
    {
        if (_byHandle.TryGetValue(handle, out var agent))
        {
            mission = agent.Mission;
            return true;
        }
        mission = default!;
        return false;
    }

    /// <summary>Resolve a handle to its directory metadata (provenance, scope, seal).</summary>
    public bool TryResolveDescriptor(string handle, out AgentDescriptor descriptor)
    {
        if (_byHandle.TryGetValue(handle, out var agent))
        {
            descriptor = agent.Descriptor;
            return true;
        }
        descriptor = default!;
        return false;
    }

    /// <summary>
    /// Every registered agent, ordered by handle — the data behind the <c>/agents</c> directory
    /// (38.5 task 9). Scope-filtering by the addressing user arrives with persisted scopes;
    /// built-ins are all <see cref="AgentScope.Shared"/> today.
    /// </summary>
    public IReadOnlyList<AgentDescriptor> List() =>
        _byHandle.Values
            .Select(a => a.Descriptor)
            .OrderBy(d => d.Handle, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private readonly record struct RegisteredAgent(AgentDescriptor Descriptor, MissionEntry Mission);
}
