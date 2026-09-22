using ForgeMission.MissionRegistry;

namespace ForgeMission.Runner;

/// <summary>A Runner-owned baked mission and its optional OCI source.</summary>
internal sealed record BuiltinMission(string Label, string Description, string? OciRef, string LocalDir);

/// <summary>Runner-owned availability and fallback policy for baked mission content.</summary>
internal static class BuiltinMissions
{
    private const string Registry = "ghcr.io/katasec";

    public static readonly IReadOnlyList<BuiltinMission> All =
    [
        new("ChatGPT", "Raw LLM — no verification", BuiltinMissionReferences.Vanilla, "vanilla"),
        new("Forge", "LLM + deterministic verifier, retries on fail",
            $"{Registry}/forge-mission-hallucination-guard@sha256:4020a035d14b00a76f723e1c147205316cbbba81c627e6fd6c920d0feddd3424", "hallucination-guard"),
        new("Assistant", "General assistant, answers LLM-verified",
            $"{Registry}/forge-mission-assistant@sha256:03749f67d10fe3ed9672f96afa2a138f7f89403d070a806139e946a10e62624c", "assistant"),
        new("Claude", "Raw Claude — no verification",
            $"{Registry}/forge-mission-claude@sha256:9aafb6d2ed23616ebe8b6460012f36df0d81d12f588e341cb830d33e77e77aca", "claude"),
        new("Grok", "Grok with live web search — classifies, searches when current data is needed, grounds the answer",
            $"{Registry}/forge-mission-grok@sha256:a18d65a6a0891f82684a01e8e038b5653cac06673ff6ded556bcbd4448dba585", "grok"),
        new("WebSearch", "Grounded, source-cited answers via live web search",
            $"{Registry}/forge-mission-websearch@sha256:dc69d92b53cf0fbb28f0e241568eaa716ab3215f326a7ba72acd62b666d0478d", "websearch"),
        new("Ocr", "Deterministic OCR artifact demo — accepts an uploaded image/PDF and returns text or PDF output", null, "ocr"),
        new("Summarize", "OCR + verified LLM synthesis — accepts an uploaded image/PDF and returns a grounded summary", null, "summarize"),
    ];

    public static async Task<List<(string label, string description, string path)>> ResolveAsync(
        string bakedInDirectory,
        CancellationToken cancellationToken = default)
    {
        var specs = new List<(string, string, string)>();
        foreach (var mission in All)
        {
            var directory = await ResolveDirectoryAsync(mission, bakedInDirectory, cancellationToken);
            specs.Add((mission.Label, mission.Description, Path.Combine(directory, "mission.mcl")));
        }

        return specs;
    }

    private static async Task<string> ResolveDirectoryAsync(
        BuiltinMission mission,
        string bakedInDirectory,
        CancellationToken cancellationToken)
    {
        var fallback = Path.Combine(bakedInDirectory, mission.LocalDir);
        if (mission.OciRef is null)
        {
            Console.Error.WriteLine($"Runner: built-in '{mission.Label}' uses baked-in mission {fallback}");
            return fallback;
        }

        try
        {
            var (directory, status) = await OciMissionPuller.PullAsync(mission.OciRef, refresh: false, cancellationToken);
            Console.Error.WriteLine($"Runner: built-in '{mission.Label}' {status} from {mission.OciRef}");
            return directory;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Runner: pull failed for '{mission.Label}' ({ex.Message}) — falling back to baked-in {fallback}");
            return fallback;
        }
    }
}
