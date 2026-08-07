using ForgeMission.ClientRuntime.TransportHost;

namespace ForgeMission.Tests.ClientRuntime;

public sealed class ClientRuntimeEndpointsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("cloud")]
    [InlineData("CLOUD")]
    public void UsesCloudMissionRuntime_CloudOrUnsetMode_ReturnsTrue(string? mode)
    {
        Assert.True(ClientRuntimeEndpoints.UsesCloudMissionRuntime(mode));
    }

    [Theory]
    [InlineData("docker")]
    [InlineData("remote")]
    public void UsesCloudMissionRuntime_DockerOrUnrecognizedMode_ReturnsFalse(string mode)
    {
        Assert.False(ClientRuntimeEndpoints.UsesCloudMissionRuntime(mode));
    }
}
