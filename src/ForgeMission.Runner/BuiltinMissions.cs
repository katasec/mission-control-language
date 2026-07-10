using ForgeMission.Cli;

namespace ForgeMission.Runner;

/// <summary>
/// The built-in missions and the immutable <c>@sha256</c> digests they are pinned to on the trusted
/// Forge registry (Phase 39.4). Digest-pinning IS the trust boundary for built-ins: the registry can
/// only return the exact content behind a digest, so a pull is self-verifying (cosign signatures
/// land in 39.5 for third-party/custom missions). Published to <c>ghcr.io/katasec</c> 2026-07-09.
/// </summary>
/// <param name="OciRef">The pinned ghcr digest to pull, or <c>null</c> for a <b>local-only</b>
/// built-in loaded straight from the baked-in copy (no pull, no fallback noise).</param>
internal sealed record BuiltinMission(string Label, string Description, string? OciRef, string LocalDir);

internal static class BuiltinMissions
{
    private const string Reg = "ghcr.io/katasec";

    public static readonly IReadOnlyList<BuiltinMission> All =
    [
        new("ChatGPT",   "Raw LLM — no verification",
            $"{Reg}/forge-mission-vanilla@sha256:aa9852074a4b196dc153e3e495b4087954d76522635430b75ae3844c50e86bd1",             "vanilla"),
        new("Forge",     "LLM + deterministic verifier, retries on fail",
            $"{Reg}/forge-mission-hallucination-guard@sha256:5854bebc975b7f81d0d03430089d69396153fb1dd5665e214364c026f4001414", "hallucination-guard"),
        new("Assistant", "General assistant, answers LLM-verified",
            $"{Reg}/forge-mission-assistant@sha256:4ba3278af7b9400e28ff20c559a4274b6546c03571e3e248a67ec03eabcddbf9",          "assistant"),
        new("Claude",    "Raw Claude — no verification",
            $"{Reg}/forge-mission-claude@sha256:5f474a569a40e156218f0f5e2644b753ac3fb6c7bb7f662099826d9d09a93adb",             "claude"),
        new("Grok",      "Raw Grok (xAI) — no verification",
            $"{Reg}/forge-mission-grok@sha256:f60b53ce79dfc23fc0774e9d0af83b1dbe806cf597bd0b6d6288a3ef0944cf6a",               "grok"),
        // INTERIM (2026-07-11): family-halaqa is registered as a built-in to prove the artifact-plane
        // UX (38.9) end-to-end. It is NOT published to ghcr — OciRef=null loads it directly from the
        // baked-in copy (Dockerfile.runner bakes missions/). Real home = the Phase 39.5 per-user custom
        // registry; move it there once the UX is reviewed/refined. (This is the one local exception; the
        // other five stay ghcr-pulled by pinned digest.)
        new("FamilyHalaqa", "Edits a slide-deck PDF (remove pages + cover), verified page-for-page",
            OciRef: null, "family-halaqa"),
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
            // INTERIM (2026-07-11): a local-only built-in (OciRef=null) loads from the baked-in copy
            // with no pull attempt — avoids a noisy pull-fail→fallback on every boot. See the
            // FamilyHalaqa note above; real home is the 39.5 custom registry.
            if (b.OciRef is null)
            {
                dir = Path.Combine(bakedInDir, b.LocalDir);
                Console.Error.WriteLine($"Runner: built-in '{b.Label}' loaded local-only from {dir}");
                specs.Add((b.Label, b.Description, Path.Combine(dir, "mission.mcl")));
                continue;
            }
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
