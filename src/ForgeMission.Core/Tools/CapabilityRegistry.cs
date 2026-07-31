using Microsoft.Extensions.AI;

namespace ForgeMission.Core.Tools;

// The Client Runtime's in-process manifest. It reports the providers available on this client;
// deciding whether an individual call is authorized is intentionally a later layer (Phase 43.9).
public sealed class CapabilityRegistry
{
    private readonly IReadOnlyList<ICapabilityProvider> _providers;

    public CapabilityRegistry(IEnumerable<ICapabilityProvider> providers)
    {
        _providers = providers.ToList();
        var duplicate = _providers.GroupBy(provider => provider.CapabilityName, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException($"Capability registered more than once: {duplicate.Key}", nameof(providers));

        AvailableCapabilities = _providers.Select(provider => provider.CapabilityName).ToList();
        ToolDeclarations = BuildToolDeclarations(_providers);
    }

    public IReadOnlyList<string> AvailableCapabilities { get; }

    // Derived from the actual provider set, rather than a fixed tool list assumed for every client.
    public IReadOnlyList<AITool> ToolDeclarations { get; }

    public bool TryGetProvider<TProvider>(out TProvider? provider)
        where TProvider : class, ICapabilityProvider
    {
        provider = _providers.OfType<TProvider>().SingleOrDefault();
        return provider is not null;
    }

    private static IReadOnlyList<AITool> BuildToolDeclarations(IEnumerable<ICapabilityProvider> providers)
    {
        var declarations = new List<AITool>();
        if (providers.OfType<IFileProvider>().Any())
            declarations.AddRange([AgentToolDeclarations.Read, AgentToolDeclarations.Edit, AgentToolDeclarations.Write]);
        if (providers.OfType<ITerminalProvider>().Any())
            declarations.Add(AgentToolDeclarations.Bash);

        return declarations;
    }
}
