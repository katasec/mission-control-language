using ForgeMission.Core.Resolution;

namespace ForgeMission.Tests.Resolution;

public sealed class MissionDiscoveryTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("forge-mission-discovery-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Discover_MissingRoot_ReturnsEmpty()
    {
        var missions = MissionDiscovery.Discover(Path.Combine(_root, "does-not-exist"));

        Assert.Empty(missions);
    }

    [Fact]
    public void Discover_DirectoryWithoutMissionFile_IsSkipped()
    {
        Directory.CreateDirectory(Path.Combine(_root, "not-a-mission"));

        var missions = MissionDiscovery.Discover(_root);

        Assert.Empty(missions);
    }

    [Fact]
    public void Discover_DirectoryWithMissionFile_IsReturned()
    {
        var missionDir = CreateMission("vanilla");

        var missions = MissionDiscovery.Discover(_root);

        var mission = Assert.Single(missions);
        Assert.Equal("vanilla", mission.Name);
        Assert.Equal(Path.Combine(missionDir, "mission.mcl"), mission.MissionFilePath);
        Assert.False(mission.HasManifest);
    }

    [Fact]
    public void Discover_MissionWithForgeToml_ReportsHasManifest()
    {
        var missionDir = CreateMission("sdlc-agent");
        File.WriteAllText(Path.Combine(missionDir, "forge.toml"), "[providers.default]\n");

        var missions = MissionDiscovery.Discover(_root);

        Assert.True(Assert.Single(missions).HasManifest);
    }

    [Fact]
    public void Discover_MultipleMissions_ReturnedSortedByName()
    {
        CreateMission("websearch");
        CreateMission("claude");
        CreateMission("assistant");

        var missions = MissionDiscovery.Discover(_root);

        Assert.Equal(["assistant", "claude", "websearch"], missions.Select(m => m.Name));
    }

    private string CreateMission(string name)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "mission.mcl"), "mission Test { }\n");
        return dir;
    }
}
