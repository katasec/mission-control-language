using ForgeMission.Core.Manifest;

namespace ForgeMission.Core.Resolution;

/// <summary>One mission found under a missions root — a directory containing <c>mission.mcl</c>.</summary>
public sealed record MissionDescriptor(string Name, string MissionFilePath, bool HasManifest);

/// <summary>
/// Local directory scan for attachable missions (Phase 43.3 task 1) — mirrors the
/// <c>&lt;missionsRoot&gt;/&lt;name&gt;/mission.mcl</c> convention <c>forge run</c>/<c>forge claude</c>
/// already resolve by name, but lists every mission under the root instead of one pinned handle.
/// Pure filesystem read, no OCI/registry lookups — those stay a later upgrade (Phase 39.4).
/// </summary>
public static class MissionDiscovery
{
    public const string MissionFileName = "mission.mcl";

    public static IReadOnlyList<MissionDescriptor> Discover(string missionsRoot)
    {
        if (!Directory.Exists(missionsRoot))
            return [];

        var results = new List<MissionDescriptor>();
        foreach (var dir in Directory.EnumerateDirectories(missionsRoot))
        {
            var missionFile = Path.Combine(dir, MissionFileName);
            if (!File.Exists(missionFile))
                continue;

            results.Add(new MissionDescriptor(
                Name: Path.GetFileName(dir),
                MissionFilePath: Path.GetFullPath(missionFile),
                HasManifest: File.Exists(Path.Combine(dir, ForgeTomlReader.FileName))));
        }

        results.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return results;
    }
}
