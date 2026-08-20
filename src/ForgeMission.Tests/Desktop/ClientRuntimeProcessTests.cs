using ForgeMission.Desktop;

namespace ForgeMission.Tests.Desktop;

// Exactly what the Supervisor tells its child. Both runtime URLs are already resolved and verified
// by the time the child is launched, so the durable one is passed every time — asserted against the
// start info rather than a spawned process.
public sealed class ClientRuntimeProcessTests
{
    [Fact]
    public void BuildStartInfo_CarriesBothRuntimesIntoTheChildEnvironment()
    {
        var startInfo = ClientRuntimeProcess.BuildStartInfo(
            "https://forge.katasec.com/", "cloud", "platform-key", "http://127.0.0.1:18080/");

        Assert.Equal("https://forge.katasec.com/", startInfo.EnvironmentVariables["MissionRuntime__BaseUrl"]);
        Assert.Equal("cloud", startInfo.EnvironmentVariables["MissionRuntime__Mode"]);
        Assert.Equal("platform-key", startInfo.EnvironmentVariables["MissionRuntime__Credential"]);
        Assert.Equal("http://127.0.0.1:18080/", startInfo.EnvironmentVariables["ConversationRuntime__BaseUrl"]);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
    }

    // The durable URL is no longer conditional: a default-derived endpoint reaches the child exactly
    // as a configured one does, so Janus can never fall back to a relative URI with no base address.
    [Fact]
    public void BuildStartInfo_SetsTheDurableUrlUnconditionally()
    {
        var fromDefault = ClientRuntimeProcess.BuildStartInfo(
            "https://forge.katasec.com/", "cloud", "platform-key", "http://127.0.0.1:18080/");
        var fromConfiguration = ClientRuntimeProcess.BuildStartInfo(
            "https://forge.katasec.com/", "cloud", "platform-key", "https://durable.forge.example/");

        Assert.Equal("http://127.0.0.1:18080/", fromDefault.EnvironmentVariables["ConversationRuntime__BaseUrl"]);
        Assert.Equal("https://durable.forge.example/", fromConfiguration.EnvironmentVariables["ConversationRuntime__BaseUrl"]);
    }
}
