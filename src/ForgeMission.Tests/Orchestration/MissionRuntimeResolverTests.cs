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

        var (baseUrl, mode, launcher) = await MissionRuntimeResolver.ResolveAsync(configuration);

        Assert.Equal("https://forge.katasec.com/", baseUrl);
        Assert.Equal("cloud", mode);
        Assert.Null(launcher);
    }

    [Fact]
    public async Task ResolveAsync_CloudWithoutConfiguredUrl_UsesEndpointConvention()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new("MissionRuntime:Mode", "cloud")])
            .Build();

        var (baseUrl, mode, launcher) = await MissionRuntimeResolver.ResolveAsync(configuration);

        Assert.Equal(MissionRuntimeResolver.ResolveCloudBaseUrl(null,
            Environment.GetEnvironmentVariable("FORGE_API_ENDPOINT")), baseUrl);
        Assert.Equal("cloud", mode);
        Assert.Null(launcher);
    }

    [Fact]
    public async Task ResolveAsync_WithoutMode_UsesCloudDefault()
    {
        var configuration = new ConfigurationBuilder().Build();

        var (baseUrl, mode, launcher) = await MissionRuntimeResolver.ResolveAsync(configuration);

        Assert.Equal(MissionRuntimeResolver.ResolveCloudBaseUrl(null,
            Environment.GetEnvironmentVariable("FORGE_API_ENDPOINT")), baseUrl);
        Assert.Equal("cloud", mode);
        Assert.Null(launcher);
    }

    [Fact]
    public async Task ResolveAsync_UnrecognizedModeWithoutConfiguredUrl_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new("MissionRuntime:Mode", "remote")])
            .Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() => MissionRuntimeResolver.ResolveAsync(configuration));
    }

    [Fact]
    public void ResolveCloudBaseUrl_UsesEnvironmentOverrideWithoutTrailingSlash()
    {
        var baseUrl = MissionRuntimeResolver.ResolveCloudBaseUrl(null, "https://forge.example/");

        Assert.Equal("https://forge.example", baseUrl);
    }

    [Fact]
    public void ResolveCloudBaseUrl_UsesDefaultWhenNoUrlsAreConfigured()
    {
        var baseUrl = MissionRuntimeResolver.ResolveCloudBaseUrl(null, null);

        Assert.Equal(MissionRuntimeResolver.DefaultCloudEndpoint, baseUrl);
    }

    [Fact]
    public void ResolveCloudBaseUrl_PreservesConfiguredUrl()
    {
        var baseUrl = MissionRuntimeResolver.ResolveCloudBaseUrl("https://configured.example/", "https://ignored.example/");

        Assert.Equal("https://configured.example/", baseUrl);
    }

    [Fact]
    public void ResolveMode_ExplicitDockerRemainsDocker()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new("MissionRuntime:Mode", "docker")])
            .Build();

        Assert.Equal("docker", MissionRuntimeResolver.ResolveMode(configuration));
    }
}
