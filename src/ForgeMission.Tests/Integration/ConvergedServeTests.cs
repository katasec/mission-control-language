using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using Katasec.AnthropicServer;
using Katasec.OaiServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ForgeMission.Tests.Integration;

// Phase 42.4 task 1: ONE app serves both wires. The Anthropic routes ("/" probe, /v1/messages)
// and the OpenAI routes (/v1/chat/completions, /v1/responses, /v1/models) are disjoint by
// construction — every door must answer on the same port, each backed by its own door client.
public sealed class ConvergedServeTests
{
    [Fact]
    public async Task AllDoorsAnswerOnOneApp()
    {
        var port = FindFreePort();
        var app  = BuildConvergedApp(port);
        await app.StartAsync();
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

            // Anthropic door: the claude CLI's HEAD / probe, then a mission request.
            var probe = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/"));
            Assert.Equal(HttpStatusCode.OK, probe.StatusCode);

            var messages = await PostAsync(http, "/v1/messages",
                """{"model":"forge","max_tokens":64,"messages":[{"role":"user","content":"run the mission"}]}""");
            Assert.Contains("ANTHROPIC DOOR", messages);

            // OpenAI door: models, chat completions, responses.
            var models = await http.GetStringAsync("/v1/models");
            Assert.Contains("converged", models);

            var chat = await PostAsync(http, "/v1/chat/completions",
                """{"model":"forge","messages":[{"role":"user","content":"hi"}]}""");
            Assert.Contains("OPENAI DOOR", chat);

            var responses = await PostAsync(http, "/v1/responses", """{"input":"hi"}""");
            Assert.Contains("OPENAI DOOR", responses);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    // Mirrors ForgeServe.MapWires' composition, bound to loopback (binding 0.0.0.0 under
    // `dotnet test` trips the macOS firewall prompt). The Serve lib pins these servers from
    // NuGet while Tests build them from the sibling repo source — the two copies can't share
    // one compile graph, so the composition is mirrored here rather than referenced.
    private static WebApplication BuildConvergedApp(int port)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseSetting("urls", $"http://127.0.0.1:{port}");
        builder.Logging.ClearProviders();

        var app = builder.Build();
        new AnthropicServer(new StaticReplyClient("ANTHROPIC DOOR"), "converged").Map(app);
        new OaiServer(new StaticReplyClient("OPENAI DOOR"), new InMemorySessionStore(), "converged").Map(app);
        return app;
    }

    private static async Task<string> PostAsync(HttpClient http, string route, string json)
    {
        var response = await http.PostAsync(route, new StringContent(json, Encoding.UTF8, "application/json"));
        var body     = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{route} → HTTP {(int)response.StatusCode}: {body}");
        return body;
    }

    private static int FindFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class StaticReplyClient(string reply) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
            => Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, reply)]));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var response = await GetResponseAsync(messages, options, ct);
            yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text);
        }

        public void Dispose() { }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
    }
}
