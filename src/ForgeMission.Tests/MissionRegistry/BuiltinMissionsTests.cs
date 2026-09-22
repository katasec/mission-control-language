using ForgeMission.MissionRegistry;
using ForgeMission.Core.Resolution;

namespace ForgeMission.Tests.MissionRegistry;

public sealed class BuiltinMissionsTests
{
    [Fact]
    public void Summarize_is_registered_as_baked_in_builtin()
    {
        var summarize = Assert.Single(BuiltinMissions.All, b => b.Label == "Summarize");

        Assert.Null(summarize.OciRef);
        Assert.Equal("summarize", summarize.LocalDir);
    }

    [Fact]
    public void Vanilla_is_a_digest_pinned_reference()
    {
        Assert.Equal(
            "ghcr.io/katasec/forge-mission-vanilla@sha256:9663e05847676da28191f09459ce45671d624221d2d9b329ff0770cb9621dc46",
            BuiltinMissionReferences.Vanilla);
    }

    [Fact]
    public void Unpinned_reference_is_rejected_by_the_default_resource_validator()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => MissionSourceSelection.ValidateMissionRef("ghcr.io/katasec/forge-mission-vanilla:latest"));

        Assert.Contains("digest-pinned OCI reference", exception.Message);
    }
}
