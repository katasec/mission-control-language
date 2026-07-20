using ForgeMission.Core.Experts;
using ForgeMission.Core.Manifest;
using ForgeMission.Core.Resolution;
using ForgeMission.Runner.Contracts;
using ForgeMission.Parser;
using MclProgram = ForgeMission.Parser.Program;

namespace ForgeMission.Runner;

/// <summary>
/// A baked-in mission the runner can execute. Unlike ForgeUI's <c>MissionEntry</c>, this keeps the
/// <see cref="ProviderProfile"/> (not a pre-built runner) so a <em>fresh</em> usage-tracked runner
/// is constructed per request — one <see cref="Core.Adapters.UsageAccumulator"/> per run, so token
/// counts never bleed across the warm runner's concurrent runs.
/// </summary>
internal sealed record RunnerMission(
    string                               Label,
    string                               Description,
    MclProgram                           Ast,
    Dictionary<string, ExpertDefinition> Experts,
    ProviderProfile?                     Profile,
    MissionArtifactCapabilities?         ArtifactCapabilities);

/// <summary>
/// The runner's own mission registry, loaded once at boot from the missions baked into the image
/// (39.1 — no OCI/blob yet). Mirrors <c>ForgeUI.Services.MissionRegistry.LoadAsync</c>, including
/// the per-mission key gate: a mission whose provider has no API key is skipped, so <c>GET
/// /missions</c> never advertises it and the orchestrator won't bind its handle (preserving the
/// "@claude only appears when ANTHROPIC_API_KEY is set" behaviour, now that keys live here).
/// </summary>
internal sealed class RunnerRegistry
{
    private readonly Dictionary<string, RunnerMission> _byLabel;

    private RunnerRegistry(Dictionary<string, RunnerMission> byLabel) => _byLabel = byLabel;

    public IReadOnlyCollection<RunnerMission> All => _byLabel.Values;

    public bool TryGet(string missionRef, out RunnerMission mission) =>
        _byLabel.TryGetValue(missionRef, out mission!);

    public static async Task<RunnerRegistry> LoadAsync(
        IEnumerable<(string label, string description, string path)> specs)
    {
        var byLabel = new Dictionary<string, RunnerMission>(StringComparer.Ordinal);

        foreach (var (label, description, path) in specs)
        {
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"Runner: mission file not found for '{label}' at {path} — skipping.");
                continue;
            }

            var dir      = Path.GetDirectoryName(Path.GetFullPath(path))!;
            var source   = await File.ReadAllTextAsync(path);
            var ast      = MclParser.Parse(source);
            var lockFile = LockFileIO.Read(Path.Combine(dir, "mcl.lock"));
            var experts  = ExpertResolver.ResolveAll(lockFile, dir, verbose: null, warnings: Console.Error);
            try
            {
                // Future request-time/custom mission load paths must also validate before registration;
                // PipelineRunner intentionally has no runtime mission-recursion depth guard.
                ExpertLoader.Validate(ast, experts, Console.Error, contractErrorsAreFatal: true, path);
            }
            catch (AggregateExpertLoadException ex)
            {
                foreach (var error in ex.Errors)
                    Console.Error.WriteLine($"Runner: '{label}' validation failed: {error.Message}");
                continue;
            }
            catch (ExpertLoadException ex)
            {
                Console.Error.WriteLine($"Runner: '{label}' validation failed: {ex.Message}");
                continue;
            }
            var manifest = ForgeTomlReader.TryRead(path);

            ProviderProfile? profile = null;
            if (manifest?.Providers?.GetValueOrDefault("default") is { } p)
            {
                if (string.IsNullOrWhiteSpace(p.ApiKey))
                {
                    // No key for this provider (e.g. ANTHROPIC_API_KEY unset) — skip so the handle
                    // simply won't bind, rather than advertise a mission that fails at call time.
                    Console.Error.WriteLine(
                        $"Runner: '{label}' has no API key for provider '{p.Provider}' — skipping.");
                    continue;
                }
                profile = p;
            }
            else
            {
                Console.Error.WriteLine($"Runner: no forge.toml for '{label}' — LLM steps won't work.");
            }

            byLabel[label] = new RunnerMission(
                label,
                description,
                ast,
                experts,
                profile,
                ToContractCapabilities(manifest?.Capabilities.Artifacts));
        }

        return new RunnerRegistry(byLabel);
    }

    private static MissionArtifactCapabilities? ToContractCapabilities(ArtifactCapabilities? artifacts)
    {
        if (artifacts is null || artifacts.Inputs.Count == 0)
            return null;

        return new MissionArtifactCapabilities(
            artifacts.Inputs
                .Select(i => new MissionArtifactInputCapability(
                    i.Key,
                    i.Value.ContentTypes,
                    i.Value.MaxSizeMb))
                .ToList());
    }
}
