using System.Runtime.CompilerServices;
using System.Text.Json;
using ForgeMission.ConversationWorker.Janus;
using ForgeMission.Core.Adapters;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Manifest;
using ForgeMission.Core.Runtime;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Parser;
using Microsoft.Extensions.AI;

namespace ForgeMission.ConversationWorker.Tests;

/// <summary>
/// Proves that <see cref="JanusMissionExecutor"/> itself — not just <see cref="PipelineRunOptions"/>'s
/// default — supplies <c>AllowMultipleToolCalls: false</c> on the Implementer's real provider
/// request, for both the initial run and a tool-result continuation (Phase 43.16 Task 8b). Uses a
/// real <see cref="DirectExpertRunner"/> wrapping a local capturing fake <see cref="IChatClient"/>
/// (provider-free, no network) rather than the <c>FakeExpertRunner</c> used elsewhere in this
/// project — that fake never constructs a real <see cref="ChatOptions"/>, so it cannot prove this.
/// Deliberately uses its own local synthetic mission source/expert dictionary rather than
/// <c>MissionCommandProcessorTests</c>'s, which are private to that class.
/// </summary>
public class JanusMissionExecutorToolCallOptionsTests
{
    private const string MissionSource = """
        mission Negotiate(task) loop(5) = {
            Proposer using implementer
            -> Approver using implementer
        }

        mission Implement(plan) = {
            Implementer using implementer
        }
        """;

    private static Dictionary<string, ExpertDefinition> Experts() => new(StringComparer.Ordinal)
    {
        ["Proposer"] = new("Proposer", "in", "out", "prompt", Role: ""),
        ["Approver"] = new("Approver", "in", "out", "prompt", Role: "judge"),
        ["Implementer"] = new("Implementer", "in", "out", "prompt", Role: "agent"),
    };

    private static ConversationCapabilityDeclaration OneDeclaredCapability() =>
        new("Read", "Reads a file", JsonDocument.Parse("""{"type":"object"}""").RootElement);

    private static JanusMissionContext BuildMission(IChatClient client) => new()
    {
        Ast = MclParser.Parse(MissionSource),
        Experts = Experts(),
        Runners = new Dictionary<string, IExpertRunner>(StringComparer.Ordinal)
        {
            ["implementer"] = new DirectExpertRunner(client),
        },
        Execution = new ExecutionConfig(),
    };

    [Fact]
    public async Task RunFullMissionAsync_SetsAllowMultipleToolCallsFalse_OnTheImplementerCall()
    {
        var client = new CapturingChatClient();
        var mission = BuildMission(client);

        await JanusMissionExecutor.RunFullMissionAsync(
            mission, goal: "do the task", capabilities: [OneDeclaredCapability()],
            publishFactAsync: (_, _) => Task.CompletedTask,
            onApprovedPlanAsync: (_, _) => Task.CompletedTask,
            ct: default);

        var implementerCall = client.Calls.Single(c => c.Options?.Tools is { Count: > 0 });
        Assert.False(implementerCall.Options?.AllowMultipleToolCalls);
    }

    [Fact]
    public async Task RunContinuationAsync_SetsAllowMultipleToolCallsFalse_OnTheResumedImplementerCall()
    {
        var client = new CapturingChatClient();
        var mission = BuildMission(client);

        await JanusMissionExecutor.RunContinuationAsync(
            mission, approvedPlan: "the approved plan",
            providerCallId: "provider-call-1", toolName: "Read",
            toolArguments: JsonDocument.Parse("""{"file_path":"a.txt"}""").RootElement,
            toolResult: new ConversationToolResult(Guid.NewGuid(), "file contents", IsError: false),
            capabilities: [OneDeclaredCapability()],
            publishFactAsync: (_, _) => Task.CompletedTask,
            ct: default);

        var implementerCall = Assert.Single(client.Calls);
        Assert.False(implementerCall.Options?.AllowMultipleToolCalls);
    }

    // Envelope JSON for Proposer/Approver; a plain text final answer once tools are attached
    // (Implementer) — deliberately not a tool-call reply, so both tests reach a clean terminal
    // result without needing a further round trip.
    private sealed class CapturingChatClient : IChatClient
    {
        public List<(IList<ChatMessage> Messages, ChatOptions? Options)> Calls { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            Calls.Add((messages.ToList(), options));

            var reply = options?.Tools is { Count: > 0 }
                ? new ChatResponse([new ChatMessage(ChatRole.Assistant, "final answer")])
                : new ChatResponse([new ChatMessage(ChatRole.Assistant,
                    """{"text": "step output", "status": "pass", "reason": null}""")]);

            return Task.FromResult(reply);
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
