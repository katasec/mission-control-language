using ForgeMission.Orchestration;
using Microsoft.Extensions.Configuration;

namespace ForgeMission.Tests.Orchestration;

public sealed class MissionRuntimeResolverTests
{
    [Fact]
    public async Task ResolveAsync_NonDockerModeWithConfiguredUrl_ReturnsUrlWithoutLauncher()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
            [
                new("MissionRuntime:Mode", "cloud"),
                new("MissionRuntime:BaseUrl", "https://forge.katasec.com/"),
            ])
            .Build();

        var (baseUrl, launcher) = await MissionRuntimeResolver.ResolveAsync(configuration);

        Assert.Equal("https://forge.katasec.com/", baseUrl);
        Assert.Null(launcher);
    }

    [Fact]
    public async Task ResolveAsync_NonDockerModeWithoutConfiguredUrl_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new("MissionRuntime:Mode", "cloud")])
            .Build();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => MissionRuntimeResolver.ResolveAsync(configuration));
    }
}
