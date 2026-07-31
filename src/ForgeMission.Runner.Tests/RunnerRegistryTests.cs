using ForgeMission.Runner;

namespace ForgeMission.Runner.Tests;

public sealed class RunnerRegistryTests : IDisposable
{
    private const string MissionRef =
        "ghcr.io/katasec/forge-mission-vanilla@sha256:9663e05847676da28191f09459ce45671d624221d2d9b329ff0770cb9621dc46";

    private readonly string _missionDirectory =
        Path.Combine(Path.GetTempPath(), $"forge-runner-registry-{Guid.NewGuid():N}");

    public RunnerRegistryTests()
    {
        Directory.CreateDirectory(_missionDirectory);
    }

    [Fact]
    public async Task LoadAsync_ResolvedMissionRefWithValidationFailure_ThrowsNamedStartupError()
    {
        var missionPath = Path.Combine(_missionDirectory, "mission.mcl");
        await File.WriteAllTextAsync(missionPath, """
            mission Broken(goal) = {
              MissingExpert
            }

            output(Broken)
            """);
        await File.WriteAllTextAsync(Path.Combine(_missionDirectory, "mcl.lock"), "version: 1\nexperts: {}\n");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunnerRegistry.LoadAsync(
                [("vanilla", "OCI mission", missionPath)],
                requiredMissionRef: MissionRef));

        Assert.Contains(MissionRef, exception.Message, StringComparison.Ordinal);
        Assert.Contains("validation failed", exception.Message, StringComparison.Ordinal);
        Assert.Contains("MissingExpert", exception.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        Directory.Delete(_missionDirectory, recursive: true);
    }
}
