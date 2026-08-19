using System.Net;
using ForgeMission.Orchestration;

namespace ForgeMission.Tests.Orchestration;

// Readiness is exactly one observation — GET {baseUrl}health, the route ConversationHost actually
// maps — and an unreachable endpoint is an answer rather than an exception, so the bootstrap can
// decide whether to start its own tunnel.
public sealed class ConversationRuntimeReadinessProbeTests
{
    private const string BaseUrl = ConversationRuntimeResolver.DefaultLocalBaseUrl;

    [Fact]
    public async Task IsHealthyAsync_ProbesTheExactHealthRoute_AndAcceptsSuccess()
    {
        var endpoint = StubHealthEndpoint.Always(HttpStatusCode.OK);

        Assert.True(await endpoint.NewProbe().IsHealthyAsync(BaseUrl, CancellationToken.None));

        var request = Assert.Single(endpoint.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://127.0.0.1:18080/health", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task IsHealthyAsync_UnsuccessfulStatus_IsNotHealthy()
    {
        var endpoint = StubHealthEndpoint.Always(HttpStatusCode.ServiceUnavailable);

        Assert.False(await endpoint.NewProbe().IsHealthyAsync(BaseUrl, CancellationToken.None));
    }

    [Fact]
    public async Task IsHealthyAsync_UnreachableEndpoint_ReturnsFalseRatherThanThrowing()
    {
        var endpoint = StubHealthEndpoint.Unreachable();

        Assert.False(await endpoint.NewProbe().IsHealthyAsync(BaseUrl, CancellationToken.None));
    }

    [Fact]
    public async Task EnsureHealthyAsync_WithinBudget_ReturnsOnceHealthy()
    {
        var endpoint = StubHealthEndpoint.HealthyFromCall(2);

        await endpoint.NewProbe().EnsureHealthyAsync(BaseUrl, TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Equal(2, endpoint.Requests.Count);
    }

    [Fact]
    public async Task EnsureHealthyAsync_NeverHealthy_ThrowsNamingTheEndpointAndPrerequisite()
    {
        var endpoint = StubHealthEndpoint.Unreachable();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => endpoint.NewProbe().EnsureHealthyAsync(BaseUrl, TimeSpan.Zero, CancellationToken.None));

        Assert.Contains("http://127.0.0.1:18080/health", error.Message);
        Assert.Contains("350-conversation-kind-up", error.Message);
    }

    [Fact]
    public void StartupBudget_IsTheOneFixedThirtySecondDesktopBudget()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), ConversationRuntimeReadinessProbe.StartupBudget);
    }
}
