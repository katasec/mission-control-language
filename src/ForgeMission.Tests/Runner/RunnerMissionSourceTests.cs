using ForgeMission.Core.Resolution;

namespace ForgeMission.Tests.Runner;

public sealed class MissionSourceSelectionTests
{
    private const string VanillaRef = "ghcr.io/katasec/forge-mission-vanilla@sha256:9663e05847676da28191f09459ce45671d624221d2d9b329ff0770cb9621dc46";

    [Fact]
    public void ValidateMissionRef_AcceptsPinnedGhcrReference()
        => MissionSourceSelection.ValidateMissionRef(VanillaRef);

    [Fact]
    public void Create_SelectsOciWithoutAnyFileSource()
    {
        var selection = MissionSourceSelection.Create(VanillaRef, null);

        Assert.True(selection.UsesOci);
        Assert.False(selection.UsesFile);
        Assert.Equal(VanillaRef, selection.MissionRef);
    }

    [Theory]
    [InlineData("ghcr.io/katasec/forge-mission-vanilla:latest")]
    [InlineData("ghcr.io/katasec/forge-mission-vanilla@sha256:ABC")]
    [InlineData("forge-mission-vanilla@sha256:9663e05847676da28191f09459ce45671d624221d2d9b329ff0770cb9621dc46")]
    public void ValidateMissionRef_RejectsMutableOrMalformedReference(string missionRef)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => MissionSourceSelection.ValidateMissionRef(missionRef));

        Assert.Contains("MissionRef", exception.Message);
    }

    [Fact]
    public void Create_RejectsMissionFileAndMissionRefTogether()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            MissionSourceSelection.Create(VanillaRef, "/tmp/mission.mcl"));

        Assert.Contains("either MissionRef or MissionFile", exception.Message);
    }
}
