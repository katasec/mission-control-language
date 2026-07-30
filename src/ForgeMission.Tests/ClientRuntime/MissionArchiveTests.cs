using System.Formats.Tar;
using ForgeMission.ClientRuntime.Services;

namespace ForgeMission.Tests.ClientRuntime;

public sealed class MissionArchiveTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), $"forge-mission-archive-{Guid.NewGuid():N}");

    [Fact]
    public void Create_ContainsOnlyTheSelectedMissionDirectory()
    {
        var missionDirectory = Path.Combine(tempDirectory, "missions", "sample");
        Directory.CreateDirectory(Path.Combine(missionDirectory, "experts", "Answerer"));
        File.WriteAllText(Path.Combine(missionDirectory, "mission.mcl"), "mission Sample {}");
        File.WriteAllText(Path.Combine(missionDirectory, "forge.toml"), "[providers.default]");
        File.WriteAllText(Path.Combine(missionDirectory, "experts", "Answerer", "expert.md"), "# Answerer");
        File.WriteAllText(Path.Combine(tempDirectory, "workspace-secret.txt"), "must not cross the boundary");

        using var archive = MissionArchive.Create(Path.Combine(missionDirectory, "mission.mcl"), "forge-mission");
        using var reader = new TarReader(archive);
        var entryNames = new List<string>();
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
            entryNames.Add(entry.Name);

        Assert.Contains("forge-mission/mission.mcl", entryNames);
        Assert.Contains("forge-mission/forge.toml", entryNames);
        Assert.Contains("forge-mission/experts/Answerer/expert.md", entryNames);
        Assert.DoesNotContain(entryNames, name => name.Contains("workspace-secret", StringComparison.Ordinal));
    }

    [Fact]
    public void Create_RejectsSymbolicLinks()
    {
        Skip.If(OperatingSystem.IsWindows(), "Creating symbolic links requires developer-mode privileges on Windows.");
        var missionDirectory = Path.Combine(tempDirectory, "missions", "sample");
        Directory.CreateDirectory(missionDirectory);
        File.WriteAllText(Path.Combine(missionDirectory, "mission.mcl"), "mission Sample {}");
        File.WriteAllText(Path.Combine(tempDirectory, "workspace-secret.txt"), "must not cross the boundary");
        File.CreateSymbolicLink(
            Path.Combine(missionDirectory, "linked-secret.txt"),
            Path.Combine(tempDirectory, "workspace-secret.txt"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MissionArchive.Create(Path.Combine(missionDirectory, "mission.mcl"), "forge-mission"));

        Assert.Contains("symbolic link", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
            Directory.Delete(tempDirectory, recursive: true);
    }
}
