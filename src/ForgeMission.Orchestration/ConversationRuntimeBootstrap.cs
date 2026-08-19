using Microsoft.Extensions.Configuration;

namespace ForgeMission.Orchestration;

// What a prepared durable Conversation Runtime is: the base URL Client Runtime should be given, and
// the local tunnel this bootstrap started, if it started one. Disposing the lease stops only that
// tunnel — an endpoint the Supervisor merely found healthy was not created by this process and is
// never stopped by it.
public sealed class ConversationRuntimeLease(string baseUrl, IAsyncDisposable? ownedTunnel) : IAsyncDisposable
{
    public string BaseUrl { get; } = baseUrl;

    internal IAsyncDisposable? OwnedTunnel { get; } = ownedTunnel;

    public ValueTask DisposeAsync() => OwnedTunnel?.DisposeAsync() ?? ValueTask.CompletedTask;
}

// Composes resolution, readiness and the local tunnel into the one thing the Supervisor needs
// before it starts Client Runtime: a verified durable base URL. A configured endpoint is
// health-checked but never causes a local tunnel to start.
public static class ConversationRuntimeBootstrap
{
    public static async Task<ConversationRuntimeLease> PrepareAsync(
        IConfiguration configuration, CancellationToken ct = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        return await PrepareAsync(
            configuration,
            new ConversationRuntimeReadinessProbe(http),
            () => LocalKindConversationRuntimeTunnel.Start(),
            ConversationRuntimeReadinessProbe.StartupBudget,
            ct);
    }

    internal static async Task<ConversationRuntimeLease> PrepareAsync(
        IConfiguration configuration,
        ConversationRuntimeReadinessProbe probe,
        Func<IAsyncDisposable> startTunnel,
        TimeSpan budget,
        CancellationToken ct)
    {
        var endpoint = ConversationRuntimeResolver.Resolve(configuration);

        // Someone else runs a configured endpoint: verify it, never start or stop anything for it.
        if (!endpoint.IsLocalDefault)
        {
            await probe.EnsureHealthyAsync(endpoint.BaseUrl, budget, ct);
            return new ConversationRuntimeLease(endpoint.BaseUrl, ownedTunnel: null);
        }

        // A local default that is already healthy belongs to whoever started it.
        if (await probe.IsHealthyAsync(endpoint.BaseUrl, ct))
            return new ConversationRuntimeLease(endpoint.BaseUrl, ownedTunnel: null);

        var tunnel = startTunnel();
        try
        {
            await probe.EnsureHealthyAsync(endpoint.BaseUrl, budget, ct);
        }
        catch
        {
            await tunnel.DisposeAsync();
            throw;
        }

        return new ConversationRuntimeLease(endpoint.BaseUrl, tunnel);
    }
}
