using System.Net;
using System.Net.Sockets;
using Katasec.AnthropicServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ForgeMission.Tests.Integration;

internal sealed class AnthropicServerFixture : IAsyncDisposable
{
    private readonly WebApplication _app;

    public int Port { get; }
    public string BaseUrl { get; }

    private AnthropicServerFixture(WebApplication app, int port)
    {
        _app    = app;
        Port    = port;
        BaseUrl = $"http://localhost:{port}";
    }

    public static async Task<AnthropicServerFixture> StartAsync(
        IChatClient chatClient,
        string modelId = "forge",
        IChatClient? auxClient = null)
    {
        var port    = FindFreePort();
        var server  = new AnthropicServer(chatClient, modelId, auxClient);
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseSetting("urls", $"http://127.0.0.1:{port}");
        builder.Logging.ClearProviders();

        var app = builder.Build();
        server.Map(app);

        await app.StartAsync();
        return new AnthropicServerFixture(app, port);
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
