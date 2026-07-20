namespace ForgeMission.Core.Manifest;

public sealed class ForgeManifest
{
    // Expert name → OCI reference (e.g. "ghcr.io/katasec/forge-k8s-architect@0.1.0")
    // Only OCI experts are declared here. Local experts are resolved by name, no declaration needed.
    public IReadOnlyDictionary<string, string> Experts { get; init; }
        = new Dictionary<string, string>();

    // Profile name → provider config. "default" is always required when steps use an LLM.
    public IReadOnlyDictionary<string, ProviderProfile> Providers { get; init; }
        = new Dictionary<string, ProviderProfile>();

    // [execution] section — operator-level config for kind:exec experts.
    public ExecutionConfig Execution { get; init; } = new();

    // [capabilities.*] sections — package/catalog/runtime capability metadata.
    public CapabilityConfig Capabilities { get; init; } = new();
}

public sealed class ExecutionConfig
{
    // Execution backend for kind:exec experts. Only "process" is supported in the AOT CLI binary.
    // Future backends (wasm, hyperlight) require the Forge Runtime Platform (Phase 31).
    public string Backend        { get; init; } = "process";

    // Default timeout for kind:exec experts. Overridden per-expert via the 'timeout' frontmatter field.
    public string DefaultTimeout { get; init; } = "30s";
}

public sealed class ProviderProfile
{
    public string Provider  { get; init; } = "";
    public string Model     { get; init; } = "";
    public string? ApiKey   { get; init; }
    public string? Endpoint { get; init; }
}

public sealed class CapabilityConfig
{
    public ArtifactCapabilities Artifacts { get; init; } = new();
}

public sealed class ArtifactCapabilities
{
    public IReadOnlyDictionary<string, ArtifactInputCapability> Inputs { get; init; }
        = new Dictionary<string, ArtifactInputCapability>();

    public IReadOnlyDictionary<string, ArtifactModeCapability> Modes { get; init; }
        = new Dictionary<string, ArtifactModeCapability>();
}

public sealed class ArtifactInputCapability
{
    public IReadOnlyList<string> ContentTypes { get; init; } = [];
    public int MaxSizeMb { get; init; }
}

public sealed class ArtifactModeCapability
{
    public string OutputContentType { get; init; } = "";
    public string OutputExtension   { get; init; } = "";
    public bool Default             { get; init; }
}
