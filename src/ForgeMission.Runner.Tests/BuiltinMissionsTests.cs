namespace ForgeMission.Runner.Tests;

public sealed class BuiltinMissionsTests
{
    [Fact]
    public void Summarize_is_registered_as_a_runner_baked_in_mission()
    {
        var summarize = Assert.Single(BuiltinMissions.All, mission => mission.Label == "Summarize");

        Assert.Null(summarize.OciRef);
        Assert.Equal("summarize", summarize.LocalDir);
    }
}
