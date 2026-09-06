using ForgeMission.Application;

namespace ForgeMission.Tests.Application;

public sealed class ApplicationEndpointsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("cloud")]
    [InlineData("CLOUD")]
    public void UsesCloudMissionRuntime_CloudOrUnsetMode_ReturnsTrue(string? mode)
    {
        Assert.True(ConversationService.UsesCloudMissionRuntime(mode));
    }

    [Theory]
    [InlineData("docker")]
    [InlineData("remote")]
    public void UsesCloudMissionRuntime_DockerOrUnrecognizedMode_ReturnsFalse(string mode)
    {
        Assert.False(ConversationService.UsesCloudMissionRuntime(mode));
    }
}
