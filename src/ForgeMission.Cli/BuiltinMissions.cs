namespace ForgeMission.Cli;

/// <summary>
/// The built-in missions and the immutable <c>@sha256</c> digests they are pinned to on the trusted
/// Forge registry (Phase 39.4). Digest-pinning IS the trust boundary for built-ins: the registry can
/// only return the exact content behind a digest, so a pull is self-verifying (cosign signatures
/// land in 39.5 for third-party/custom missions). Published to <c>ghcr.io/katasec</c> 2026-07-09; republished 0.2.0 with role:agent terminal experts (42.4 quick win) 2026-07-17.
/// </summary>
public sealed record BuiltinMission(string Label, string Description, string OciRef, string LocalDir);

public static class BuiltinMissions
{
    private const string Reg = "ghcr.io/katasec";

    public static readonly IReadOnlyList<BuiltinMission> All =
    [
        new("ChatGPT",   "Raw LLM — no verification",
            $"{Reg}/forge-mission-vanilla@sha256:9663e05847676da28191f09459ce45671d624221d2d9b329ff0770cb9621dc46",             "vanilla"),
        new("Forge",     "LLM + deterministic verifier, retries on fail",
            $"{Reg}/forge-mission-hallucination-guard@sha256:ece5dc79e12086c50745c62e2d299402dcc452b27e0181fd4445f8082bf9bb81", "hallucination-guard"),
        new("Assistant", "General assistant, answers LLM-verified",
            $"{Reg}/forge-mission-assistant@sha256:03749f67d10fe3ed9672f96afa2a138f7f89403d070a806139e946a10e62624c",          "assistant"),
        new("Claude",    "Raw Claude — no verification",
            $"{Reg}/forge-mission-claude@sha256:9aafb6d2ed23616ebe8b6460012f36df0d81d12f588e341cb830d33e77e77aca",             "claude"),
        new("Grok",      "Grok with live web search — classifies, searches when current data is needed, grounds the answer (41.2)",
            $"{Reg}/forge-mission-grok@sha256:a18d65a6a0891f82684a01e8e038b5653cac06673ff6ded556bcbd4448dba585",               "grok"),
        new("WebSearch", "Grounded, source-cited answers via live web search — classifies, searches when current data is needed (42.6 @websearch)",
            $"{Reg}/forge-mission-websearch@sha256:dc69d92b53cf0fbb28f0e241568eaa716ab3215f326a7ba72acd62b666d0478d",         "websearch"),
    ];

    /// <summary>
    /// Resolve each built-in to its <c>mission.mcl</c> path by pulling from the registry by pinned
    /// digest into the forge cache — the "baked-in → pulled" move. Falls back to the copy baked into
    /// the image if a pull fails (registry outage), so the runner stays up. Returns
    /// <see cref="RunnerRegistry"/> load specs.
    /// </summary>
    public static async Task<List<(string label, string description, string path)>> ResolveAsync(
        string bakedInDir, CancellationToken ct = default)
    {
        var specs = new List<(string, string, string)>();
        foreach (var b in All)
        {
            string dir;
            try
            {
                var (cacheDir, status) = await OciMissionPuller.PullAsync(b.OciRef, refresh: false, ct);
                Console.Error.WriteLine($"Runner: built-in '{b.Label}' {status} from {b.OciRef}");
                dir = cacheDir;
            }
            catch (Exception ex)
            {
                var fallback = Path.Combine(bakedInDir, b.LocalDir);
                Console.Error.WriteLine(
                    $"Runner: pull failed for '{b.Label}' ({ex.Message}) — falling back to baked-in {fallback}");
                dir = fallback;
            }
            specs.Add((b.Label, b.Description, Path.Combine(dir, "mission.mcl")));
        }
        return specs;
    }
}
