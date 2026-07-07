namespace ForgeUI.Services;

/// <summary>
/// Minimal hardcoded `@handle → mission` map (38.2). The full registry with scopes and
/// save-as-agent is 38.5; here a handful of built-in agents are bound to loaded missions.
/// Handles match the seeded agent members' display names exactly.
/// </summary>
public sealed class AgentCatalog
{
    private readonly Dictionary<string, MissionEntry> _byHandle = new(StringComparer.OrdinalIgnoreCase);

    public AgentCatalog(MissionRegistry registry)
    {
        // @forge/hallucination-guard → the verify-loop mission (registry label "Forge").
        var guard = registry.Missions.FirstOrDefault(m => m.Label == "Forge");
        if (guard is not null)
            _byHandle["@forge/hallucination-guard"] = guard;

        // @forge/assistant → general assistant whose answers are LLM-verified (label "Assistant").
        // This is the default agent in every new user's starter room.
        var assistant = registry.Missions.FirstOrDefault(m => m.Label == "Assistant");
        if (assistant is not null)
            _byHandle["@forge/assistant"] = assistant;
    }

    public bool TryResolve(string handle, out MissionEntry mission)
        => _byHandle.TryGetValue(handle, out mission!);
}
