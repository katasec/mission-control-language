using ForgeMission.Core.Adapters;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Runtime;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace ForgeMission.Tests.Adapters;

/// <summary>
/// Unit tests for DirectExpertRunner role-based pass enforcement.
/// Uses a stub IChatClient — no LLM required.
/// </summary>
public class DirectExpertRunnerTests
{
    // Stub that returns a predetermined StepEnvelope JSON response.
    private sealed class StubChatClient(string status, string text = "stub output") : IChatClient
    {
        public ChatClientMetadata Metadata => new("stub", null, null);

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var json = status == "fail"
                ? $$$"""{"text":"{{{text}}}","status":"fail","reason":"stub reason"}"""
                : $$$"""{"text":"{{{text}}}","status":"pass"}""";

            var msg = new ChatMessage(ChatRole.Assistant, json);
            return Task.FromResult(new ChatResponse([msg]));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private static ExpertDefinition CriticExpert() =>
        new("PitchCritic", "draft", "critique", "You are a critic.");

    private static ExpertDefinition JudgeExpert() =>
        new("QualityJudge", "explanation", "verdict", "You are a judge.", Role: "judge");

    private static Dictionary<string, object> EmptyContext() =>
        new Dictionary<string, object> { ["output"] = "some input" };

    [Fact]
    public async Task NonJudge_LlmReturnsFail_RunnerForcesPass()
    {
        var runner   = new DirectExpertRunner(new StubChatClient("fail"));
        var envelope = await runner.RunAsync(CriticExpert(), EmptyContext());

        Assert.Equal("pass", envelope.Status);
        Assert.Null(envelope.Reason);
    }

    [Fact]
    public async Task NonJudge_LlmReturnsPass_RunnerKeepsPass()
    {
        var runner   = new DirectExpertRunner(new StubChatClient("pass"));
        var envelope = await runner.RunAsync(CriticExpert(), EmptyContext());

        Assert.Equal("pass", envelope.Status);
    }

    [Fact]
    public async Task Judge_LlmReturnsFail_RunnerPreservesFail()
    {
        var runner   = new DirectExpertRunner(new StubChatClient("fail"));
        var envelope = await runner.RunAsync(JudgeExpert(), EmptyContext());

        Assert.Equal("fail", envelope.Status);
        Assert.Equal("stub reason", envelope.Reason);
    }

    [Fact]
    public async Task Judge_LlmReturnsPass_RunnerKeepsPass()
    {
        var runner   = new DirectExpertRunner(new StubChatClient("pass"));
        var envelope = await runner.RunAsync(JudgeExpert(), EmptyContext());

        Assert.Equal("pass", envelope.Status);
    }
}
