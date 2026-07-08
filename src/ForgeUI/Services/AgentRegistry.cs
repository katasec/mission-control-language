using ForgeMission.Rooms;

namespace ForgeUI.Services;

/// <summary>
/// The Global Address List (38.5 task 1): a scoped <c>@handle → agent</c> directory that
/// replaces the hardcoded map from 38.2. Each entry is an <see cref="AgentDescriptor"/>
/// (handle + scope + owner + provenance + the <c>MissionRef</c> the runner executes).
/// <para>
/// Since Phase 39.1 the engine no longer lives in the orchestrator: the directory binds a handle to
/// a mission <em>ref</em> (a string the containerised runner resolves), not a loaded mission. At
/// boot we register only the descriptors whose ref the runner actually advertises via
/// <c>GET /missions</c> — preserving the "@claude only appears when its key is set" behaviour, with
/// the provider key now living on the runner.
/// </para>
/// <para>
/// Built-in agents are registered as <see cref="AgentScope.Shared"/> / <see cref="IdentitySeal.Official"/>
/// at startup. User-authored agents and persisted personal/room scopes arrive with save-as-agent
/// (resequenced to Phase 39.5); the descriptor model is already shaped for them.
/// </para>
/// </summary>
public sealed class AgentRegistry
{
    private readonly Dictionary<string, AgentDescriptor> _byHandle = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="availableMissionRefs">Mission refs the runner can execute (from GET /missions).
    /// A descriptor whose ref is absent is skipped, so its handle simply won't bind.</param>
    public AgentRegistry(IReadOnlySet<string> availableMissionRefs)
    {
        // @guard → the verify-loop mission (registry label "Forge").
        Register(availableMissionRefs, new AgentDescriptor
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
        Register(availableMissionRefs, new AgentDescriptor
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
        // only registers when ANTHROPIC_API_KEY is set on the runner (else its ref isn't advertised).
        Register(availableMissionRefs, new AgentDescriptor
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

        Register(availableMissionRefs, new AgentDescriptor
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

        Register(availableMissionRefs, new AgentDescriptor
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
    /// Register a descriptor if the runner advertises its mission ref. Skips silently otherwise
    /// (e.g. no forge.toml / empty API key on the runner) — matching 38.2's behaviour of simply not
    /// exposing an agent whose mission is unavailable.
    /// </summary>
    private void Register(IReadOnlySet<string> availableMissionRefs, AgentDescriptor descriptor)
    {
        if (availableMissionRefs.Contains(descriptor.MissionRef))
            _byHandle[descriptor.Handle] = descriptor;
    }

    /// <summary>Resolve a handle to its directory metadata (provenance, scope, seal, mission ref).</summary>
    public bool TryResolveDescriptor(string handle, out AgentDescriptor descriptor)
    {
        if (_byHandle.TryGetValue(handle, out var found))
        {
            descriptor = found;
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
            .OrderBy(d => d.Handle, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
