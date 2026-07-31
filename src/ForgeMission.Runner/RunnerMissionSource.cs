using ForgeMission.Cli;
using ForgeMission.Core.Resolution;
using Microsoft.Extensions.Configuration;

namespace ForgeMission.Runner;

// The Mission Runtime owns mission availability. A client chooses an immutable reference; this
// component resolves its bytes and returns the one registry spec the runner will serve.
internal static class RunnerMissionSource
{
    public static async Task<List<(string label, string description, string path)>> ResolveAsync(
        IConfiguration configuration,
        CancellationToken ct = default)
    {
        var source = MissionSourceSelection.Create(configuration["MissionRef"], configuration["MissionFile"]);

        if (source.MissionRef is { } missionRef)
            return await ResolveOciMissionAsync(missionRef, ct);

        if (source.MissionFile is { } missionFile)
            return ResolveLocalMission(missionFile);

        var missionDir = configuration["MissionDir"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "missions");
        return await BuiltinMissions.ResolveAsync(Path.GetFullPath(missionDir), ct);
    }

    private static async Task<List<(string label, string description, string path)>> ResolveOciMissionAsync(
        string missionRef,
        CancellationToken ct)
    {
        var (directory, status) = await OciMissionPuller.PullAsync(missionRef, refresh: false, ct);
        Console.Error.WriteLine($"Runner: mission {status} from {missionRef}");
        return [(LabelFrom(missionRef), "OCI mission", Path.Combine(directory, "mission.mcl"))];
    }

    private static List<(string label, string description, string path)> ResolveLocalMission(string missionFile)
    {
        var missionPath = Path.GetFullPath(missionFile);
        var label = Path.GetFileName(Path.GetDirectoryName(missionPath)) ?? "mission";
        return [(label, "local mission", missionPath)];
    }

    private static string LabelFrom(string missionRef)
    {
        var nameStart = missionRef.IndexOf('/') + 1;
        var nameEnd = missionRef.IndexOf('@');
        return missionRef[nameStart..nameEnd];
    }
}
