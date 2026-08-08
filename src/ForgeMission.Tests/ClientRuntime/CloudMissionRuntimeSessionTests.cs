using System.Net;
using System.Text;
using System.Text.Json;
using ForgeMission.ClientRuntime.Services;
using ForgeMission.Core.Runtime;
using ForgeMission.Core.Tools;

namespace ForgeMission.Tests.ClientRuntime;

public sealed class CloudMissionRuntimeSessionTests : IDisposable
{
    private readonly string _workspace = Directory.CreateTempSubdirectory("forge-cloud-session-").FullName;

    public void Dispose() => Directory.Delete(_workspace, recursive: true);

    [Fact]
    public async Task SendAsync_Replays_tool_history_within_one_token_and_uses_a_fresh_token_for_the_next_prompt()
    {
        var notesPath = Path.Combine(_workspace, "notes.txt");
        await File.WriteAllTextAsync(notesPath, "the magic word is PLATYPUS");

        var handler = new ScriptedForgeApiHandler(notesPath);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://forge.test/") };
        var session = new CloudMissionRuntimeSession(http);
        var capabilities = new CapabilityRegistry([new WorkspaceFileProvider(new LocalDiskWorkspace(_workspace))]);
        var dispatcher = new CapabilityDispatcher(
            capabilities,
            new PolicyCapabilityAuthorizer(CapabilityAuthorizationPolicy.Default),
            new InMemoryCapabilityAuditLog());
        var deltas = new List<string>();
        var toolLog = new List<string>();

        var answer = await session.SendAsync(
            "Read notes.txt and tell me the magic word.",
            capabilities,
            dispatcher,
            deltas.Add,
            notification => toolLog.Add($"{notification.State}:{notification.Call.Name}"));
        var independentAnswer = await session.SendAsync(
            "Say hello.", capabilities, dispatcher, deltas.Add);

        Assert.Equal("The magic word is PLATYPUS.", answer);
        Assert.Equal("Hello.", independentAnswer);
        Assert.Equal(["The magic word is PLATYPUS.", "Hello."], deltas);
        Assert.Equal(["Running:Read", "Done:Read"], toolLog);

        Assert.Equal(3, handler.Requests.Count);
        var first = handler.Requests[0];
        Assert.False(first.TryGetProperty("history", out _));
        Assert.Equal("Read notes.txt and tell me the magic word.", first.GetProperty("input").GetString());
        Assert.Equal("Read", first.GetProperty("tools")[0].GetProperty("name").GetString());

        var second = handler.Requests[1];
        Assert.Equal(first.GetProperty("clientToken").GetString(), second.GetProperty("clientToken").GetString());
        Assert.Equal(first.GetProperty("input").GetString(), second.GetProperty("input").GetString());
        var history = second.GetProperty("history");
        Assert.Equal(3, history.GetArrayLength());
        Assert.Equal("user", history[0].GetProperty("role").GetString());
        Assert.Equal("tool_use", history[1].GetProperty("content")[0].GetProperty("type").GetString());
        Assert.Equal("tool_result", history[2].GetProperty("content")[0].GetProperty("type").GetString());
        Assert.Contains("PLATYPUS", history[2].GetProperty("content")[0].GetProperty("toolResult").GetString());

        var third = handler.Requests[2];
        Assert.NotEqual(first.GetProperty("clientToken").GetString(), third.GetProperty("clientToken").GetString());
        Assert.Equal("Say hello.", third.GetProperty("input").GetString());
    }

    [Fact]
    public async Task SendAsync_DefaultConstructor_SendsVanillaAsTheMission()
    {
        var handler = new RecordingForgeApiHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://forge.test/") };
        var session = new CloudMissionRuntimeSession(http);
        var capabilities = new CapabilityRegistry([new WorkspaceFileProvider(new LocalDiskWorkspace(_workspace))]);
        var dispatcher = new CapabilityDispatcher(
            capabilities,
            new PolicyCapabilityAuthorizer(CapabilityAuthorizationPolicy.Default),
            new InMemoryCapabilityAuditLog());

        await session.SendAsync("Say hello.", capabilities, dispatcher);

        Assert.Equal("vanilla", handler.LastRequest!.Value.GetProperty("mission").GetString());
    }

    [Fact]
    public async Task SendAsync_MissionConstructorArgument_SendsThatMission()
    {
        var handler = new RecordingForgeApiHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://forge.test/") };
        var session = new CloudMissionRuntimeSession(http, mission: "websearch");
        var capabilities = new CapabilityRegistry([new WorkspaceFileProvider(new LocalDiskWorkspace(_workspace))]);
        var dispatcher = new CapabilityDispatcher(
            capabilities,
            new PolicyCapabilityAuthorizer(CapabilityAuthorizationPolicy.Default),
            new InMemoryCapabilityAuditLog());

        await session.SendAsync("Search for something current.", capabilities, dispatcher);

        Assert.Equal("websearch", handler.LastRequest!.Value.GetProperty("mission").GetString());
    }

    private sealed class RecordingForgeApiHandler : HttpMessageHandler
    {
        public JsonElement? LastRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await using var stream = await request.Content!.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            LastRequest = document.RootElement.Clone();

            var payload = JsonSerializer.Serialize(new { answer = "ok", responseStatus = new { } });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8,
                    System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json")),
            };
        }
    }

    private sealed class ScriptedForgeApiHandler(string notesPath) : HttpMessageHandler
    {
        public List<JsonElement> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("/api/ExecuteMission", request.RequestUri!.AbsolutePath);
            await using var stream = await request.Content!.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            Requests.Add(document.RootElement.Clone());

            var payload = Requests.Count switch
            {
                1 => JsonSerializer.Serialize(new
                {
                    answer = "",
                    toolUse = new[] { new { id = "read-1", name = "Read", arguments = ParseElement($$"""{"file_path":"{{notesPath}}"}""") } },
                    responseStatus = new { },
                }),
                2 => JsonSerializer.Serialize(new { answer = "The magic word is PLATYPUS.", responseStatus = new { } }),
                3 => JsonSerializer.Serialize(new { answer = "Hello.", responseStatus = new { } }),
                _ => throw new InvalidOperationException("Unexpected ExecuteMission call."),
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    payload,
                    Encoding.UTF8,
                    System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json")),
            };
        }
    }

    private static JsonElement ParseElement(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
