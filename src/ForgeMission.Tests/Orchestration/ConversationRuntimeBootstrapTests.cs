using System.Net;
using ForgeMission.Orchestration;
using Microsoft.Extensions.Configuration;

namespace ForgeMission.Tests.Orchestration;

// The Supervisor's durable-runtime bootstrap contract: reuse a healthy local default, start a
// tunnel only when the default is unavailable, never start one for an endpoint someone else runs,
// and dispose only the tunnel this bootstrap created.
public sealed class ConversationRuntimeBootstrapTests
{
    [Fact]
    public async Task PrepareAsync_HealthyLocalDefault_ReusesItAndStartsNoTunnel()
    {
        var endpoint = StubHealthEndpoint.Always(HttpStatusCode.OK);
        var started = 0;

        var lease = await Prepare(NoOverride(), endpoint, () => { started++; return new FakeTunnel(); });

        Assert.Equal(ConversationRuntimeResolver.DefaultLocalBaseUrl, lease.BaseUrl);
        Assert.Equal(0, started);
        Assert.Null(lease.OwnedTunnel);

        // Nothing of ours to stop: the endpoint was not created by this process.
        await lease.DisposeAsync();
    }

    [Fact]
    public async Task PrepareAsync_UnavailableLocalDefault_StartsOneTunnelAndLeasesIt()
    {
        var endpoint = StubHealthEndpoint.HealthyFromCall(2);
        var tunnel = new FakeTunnel();
        var started = 0;

        var lease = await Prepare(NoOverride(), endpoint, () => { started++; return tunnel; });

        Assert.Equal(ConversationRuntimeResolver.DefaultLocalBaseUrl, lease.BaseUrl);
        Assert.Equal(1, started);
        Assert.Same(tunnel, lease.OwnedTunnel);
        Assert.Equal(0, tunnel.DisposeCount);

        await lease.DisposeAsync();

        Assert.Equal(1, tunnel.DisposeCount);
    }

    [Fact]
    public async Task PrepareAsync_ConfiguredEndpoint_IsHealthCheckedButNeverStartsATunnel()
    {
        var endpoint = StubHealthEndpoint.Always(HttpStatusCode.OK);
        var started = 0;

        var lease = await Prepare(Override("https://durable.forge.example"), endpoint,
            () => { started++; return new FakeTunnel(); });

        Assert.Equal("https://durable.forge.example/", lease.BaseUrl);
        Assert.Equal("https://durable.forge.example/health", Assert.Single(endpoint.Requests).RequestUri!.ToString());
        Assert.Equal(0, started);
        Assert.Null(lease.OwnedTunnel);
    }

    [Fact]
    public async Task PrepareAsync_UnhealthyConfiguredEndpoint_FailsWithoutStartingATunnel()
    {
        var endpoint = StubHealthEndpoint.Unreachable();
        var started = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Prepare(Override("https://durable.forge.example/"), endpoint,
                () => { started++; return new FakeTunnel(); }));

        Assert.Equal(0, started);
    }

    [Fact]
    public async Task PrepareAsync_TunnelNeverBecomesHealthy_FailsAfterDisposingTheTunnelItStarted()
    {
        var endpoint = StubHealthEndpoint.Always(HttpStatusCode.ServiceUnavailable);
        var tunnel = new FakeTunnel();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Prepare(NoOverride(), endpoint, () => tunnel));

        Assert.Equal(1, tunnel.DisposeCount);
    }

    private static Task<ConversationRuntimeLease> Prepare(
        IConfiguration configuration, StubHealthEndpoint endpoint, Func<IAsyncDisposable> startTunnel) =>
        ConversationRuntimeBootstrap.PrepareAsync(
            configuration, endpoint.NewProbe(), startTunnel, TimeSpan.Zero, CancellationToken.None);

    private static IConfiguration NoOverride() => new ConfigurationBuilder().Build();

    private static IConfiguration Override(string baseUrl) => new ConfigurationBuilder()
        .AddInMemoryCollection([new(ConversationRuntimeResolver.ConfigurationKey, baseUrl)])
        .Build();
}
