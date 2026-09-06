using ForgeMission.Application;

namespace ForgeMission.Tests.ClientRuntime;

public sealed class ApplicationEndpointsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("cloud")]
    [InlineData("CLOUD")]
    public void UsesCloudMissionRuntime_CloudOrUnsetMode_ReturnsTrue(string? mode)
    {
        Assert.True(ApplicationInteractionServices.UsesCloudMissionRuntime(mode));
    }

    [Theory]
    [InlineData("docker")]
    [InlineData("remote")]
    public void UsesCloudMissionRuntime_DockerOrUnrecognizedMode_ReturnsFalse(string mode)
    {
        Assert.False(ApplicationInteractionServices.UsesCloudMissionRuntime(mode));
    }
}
