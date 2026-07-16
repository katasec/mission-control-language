using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Katasec.AnthropicServer;
using Microsoft.Extensions.AI;

namespace ForgeMission.Tests.Integration;

/// <summary>
/// Request classification + aux dispatch (Phase 42.3 §0), regression-tested against the
/// sanitized fixtures captured from the real claude CLI (2.1.195, 2026-07-16). Structural
/// metadata only — the fixture set IS the classifier's regression suite; re-capture and
/// re-verify on CLI version bumps.
/// </summary>
public sealed class RequestClassifierTests
{
    private static AnthropicRequest LoadFixture(string name)
    {
        var json = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "anthropic-wire", name));
        var request = JsonSerializer.Deserialize(json, AnthropicJsonContext.Default.AnthropicRequest);
        Assert.NotNull(request);
        return request;
    }

    // ------------------------------------------------------------------
    // Classification rules vs the captured fixtures
    // ------------------------------------------------------------------

    [Fact]
    public void TitleGen_ClassifiesAsAuxStructuredOutput()
        => Assert.Equal(RequestKind.AuxStructuredOutput,
            RequestClassifier.Classify(LoadFixture("aux-title-gen.json")));

    [Fact]
    public void StateCheck_ClassifiesAsAuxHousekeeping()
        => Assert.Equal(RequestKind.AuxHousekeeping,
            RequestClassifier.Classify(LoadFixture("aux-state-check.json")));

    [Fact]
    public void MainLoopUserTurn_ClassifiesAsMission()
        => Assert.Equal(RequestKind.Mission,
            RequestClassifier.Classify(LoadFixture("main-loop-4-block-user.json")));

    // Plain API clients (curl/python/42.1 chat callers) omit `thinking` entirely —
    // they must classify as Mission even with zero tools.
    [Fact]
    public void PlainClientWithoutThinkingField_ClassifiesAsMission()
    {
        var request = JsonSerializer.Deserialize(
            """{ "model": "m", "messages": [ { "role": "user", "content": "hello" } ] }""",
            AnthropicJsonContext.Default.AnthropicRequest);

        Assert.Equal(RequestKind.Mission, RequestClassifier.Classify(request!));
    }

    // ------------------------------------------------------------------
    // Dispatch over HTTP: aux never reaches the mission client
    // ------------------------------------------------------------------

    [Fact]
    public async Task TitleGen_DispatchesToAuxClient_MissionClientNeverRuns()
    {
        var mission = new CountingChatClient("MISSION REPLY");
        var aux     = new CountingChatClient("{\"title\": \"Session title\"}");
        await using var fixture = await AnthropicServerFixture.StartAsync(mission, auxClient: aux);

        var body = await PostFixtureAsync(fixture.BaseUrl, "aux-title-gen.json");

        Assert.Equal(0, mission.Calls);                       // the whole point of §0
        Assert.Equal(1, aux.Calls);
        Assert.Contains("Session title", body);
        Assert.NotNull(aux.LastOptions?.ResponseFormat);      // schema forwarded to the provider
    }

    [Fact]
    public async Task StateCheck_DispatchesToAuxClient_AsPlainPassthrough()
    {
        var mission = new CountingChatClient("MISSION REPLY");
        var aux     = new CountingChatClient("working");
        await using var fixture = await AnthropicServerFixture.StartAsync(mission, auxClient: aux);

        var body = await PostFixtureAsync(fixture.BaseUrl, "aux-state-check.json");

        Assert.Equal(0, mission.Calls);
        Assert.Equal(1, aux.Calls);
        Assert.Contains("working", body);
    }

    [Fact]
    public async Task Aux_WithoutAuxClient_GetsCannedReply_NeverTheMission()
    {
        var mission = new CountingChatClient("MISSION REPLY");
        await using var fixture = await AnthropicServerFixture.StartAsync(mission);

        var body = await PostFixtureAsync(fixture.BaseUrl, "aux-title-gen.json");

        Assert.Equal(0, mission.Calls);
        Assert.DoesNotContain("MISSION REPLY", body);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static async Task<string> PostFixtureAsync(string baseUrl, string fixtureName)
    {
        var json = await File.ReadAllTextAsync(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "anthropic-wire", fixtureName));
        using var http = new HttpClient();
        var response = await http.PostAsync($"{baseUrl}/v1/messages",
            new StringContent(json, Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {body}");
        return body;
    }

    private sealed class CountingChatClient(string reply) : IChatClient
    {
        private int _calls;
        public int Calls => _calls;
        public ChatOptions? LastOptions { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _calls);
            LastOptions = options;
            return Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, reply)]));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var response = await GetResponseAsync(messages, options, ct);
            yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text);
        }

        public void Dispose() { }
        public object? GetService(Type serviceType, object? key = null) => null;
    }
}
