using System.Net;
using System.Net.Sockets;
using Katasec.AnthropicServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ForgeMission.Tests.Application;

/// <summary>Application-owned in-process Anthropic wire fixture for legacy protocol compatibility.</summary>
internal sealed class LegacyMissionProtocolFixture : IAsyncDisposable
{
    private readonly WebApplication _app;

    private LegacyMissionProtocolFixture(WebApplication app, int port)
    {
        _app = app;
        BaseUrl = $"http://localhost:{port}";
    }

    public string BaseUrl { get; }

    public static async Task<LegacyMissionProtocolFixture> StartAsync(IChatClient chatClient)
    {
        var port = FindFreePort();
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseSetting("urls", $"http://127.0.0.1:{port}");
        builder.Logging.ClearProviders();

        var app = builder.Build();
        new AnthropicServer(chatClient, "forge").Map(app);
        await app.StartAsync();
        return new LegacyMissionProtocolFixture(app, port);
    }

    public async ValueTask DisposeAsync() => await _app.StopAsync();

    private static int FindFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
