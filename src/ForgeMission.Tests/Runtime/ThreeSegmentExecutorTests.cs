using System.Runtime.CompilerServices;
using ForgeMission.Core.Adapters;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Runtime;
using ForgeMission.Parser;
using Microsoft.Extensions.AI;

namespace ForgeMission.Tests.Runtime;

/// <summary>
/// The three-segment execution model (Phase 42.3 §segments + tasks 5-7), driven through
/// MissionChatClient exactly as the wire drives it: pre-agent runs once per user turn,
/// the agent segment resumes on tool continuations with cached enrichment restored, and
/// post-agent (Verify) runs exactly once — on the continuation whose agent segment
/// terminates without a tool call.
/// </summary>
public sealed class ThreeSegmentExecutorTests
{
    private static readonly Program Ast = MclParser.Parse("""
        mission Task(goal) = {
            Enrich
            -> Respond
            -> Verify
        }
        output(Task)
        """);

    private static Dictionary<string, ExpertDefinition> Experts() => new(StringComparer.Ordinal)
    {
        ["Enrich"]  = new("Enrich",  "any", "text", "ENRICH-PROMPT"),
        ["Respond"] = new("Respond", "any", "text", "RESPOND-PROMPT", Role: "agent"),
        ["Verify"]  = new("Verify",  "any", "text", "VERIFY-PROMPT"),
    };

    // ------------------------------------------------------------------
    // The full N+1-request tool loop: enrich once, verify once, at the right calls
    // ------------------------------------------------------------------
    [Fact]
    public async Task ToolLoop_EnrichesOncePerUserTurn_AndVerifiesOnFinalContinuation()
    {
        var provider = new SegmentAwareClient();
        var mission  = new MissionChatClient(Ast, Experts(), new DirectExpertRunner(provider), fullConversation: true);

        // Call 1 — user turn: pre-agent runs, agent calls Read → tool_use returns; Verify must NOT run.
        var turn1 = await mission.GetResponseAsync(UserTurn(), Tools());
        Assert.Single(turn1.Messages.Single().Contents.OfType<FunctionCallContent>());
        Assert.Equal(1, provider.EnrichCalls);
        Assert.Equal(0, provider.VerifyCalls);

        // Call 2 — tool continuation: agent resumes and answers; post-agent (Verify) runs NOW.
        var turn2 = await mission.GetResponseAsync(Continuation(), Tools());
        Assert.Empty(turn2.Messages.Single().Contents.OfType<FunctionCallContent>());

        Assert.Equal(1, provider.EnrichCalls);   // enrich-once: NOT re-run on the continuation
        Assert.Equal(1, provider.VerifyCalls);   // verify-runs: exactly once, on the final continuation
        Assert.Equal(2, provider.AgentCalls);    // agent ran on both calls
    }

    [Fact]
    public async Task Continuation_RestoresEnrichmentIntoTheAgentsContext()
    {
        var provider = new SegmentAwareClient();
        var mission  = new MissionChatClient(Ast, Experts(), new DirectExpertRunner(provider), fullConversation: true);

        await mission.GetResponseAsync(UserTurn(), Tools());
        await mission.GetResponseAsync(Continuation(), Tools());

        // The agent's continuation call still saw the native tool history (provider-side loop).
        var continuationCall = provider.AgentMessages[1];
        Assert.Contains(continuationCall, m => m.Contents.OfType<FunctionResultContent>().Any());
        // And the mission's own system prompt — not the client's — drives the agent.
        Assert.Equal("RESPOND-PROMPT", continuationCall.First(m => m.Role == ChatRole.System).Text);
    }

    // ------------------------------------------------------------------
    // Cache miss on a continuation ⇒ re-run pre-agent (never answer ungrounded)
    // ------------------------------------------------------------------
    [Fact]
    public async Task Continuation_OnCacheMiss_RerunsPreAgent()
    {
        var provider = new SegmentAwareClient();
        // Fresh client = fresh (empty) enrichment cache: the continuation finds nothing.
        var mission  = new MissionChatClient(Ast, Experts(), new DirectExpertRunner(provider), fullConversation: true);

        await mission.GetResponseAsync(Continuation(), Tools());

        Assert.Equal(1, provider.EnrichCalls);   // grounded: pre-agent re-ran
        Assert.Equal(1, provider.VerifyCalls);   // agent answered → post-agent ran
    }

    // ------------------------------------------------------------------
    // duplicate_continuation counter (task 7): observe-only, no replay
    // ------------------------------------------------------------------
    [Fact]
    public async Task IdenticalRequestTwice_IncrementsDuplicateContinuationCounter()
    {
        var provider = new SegmentAwareClient();
        var mission  = new MissionChatClient(Ast, Experts(), new DirectExpertRunner(provider), fullConversation: true);

        var before = MissionChatClient.DuplicateContinuations;
        await mission.GetResponseAsync(UserTurn(), Tools());
        await mission.GetResponseAsync(UserTurn(), Tools());   // identical F within the window

        Assert.True(MissionChatClient.DuplicateContinuations > before);
        Assert.Equal(2, provider.AgentCalls);                  // observe-only: both calls still ran
    }

    // ------------------------------------------------------------------
    // Repair loop: a failing Verify re-enters the agent segment, which may
    // emit a NEW tool_use — a re-entrant loop, not a one-way pipeline.
    // ------------------------------------------------------------------
    [Fact]
    public async Task FailingVerify_ReentersAgentSegment_WhichCanEmitANewToolUse()
    {
        var repairAst = MclParser.Parse("""
            mission Task(goal) loop(2) = {
                Respond
                -> Verify
            }
            output(Task)
            """);
        var experts = new Dictionary<string, ExpertDefinition>(StringComparer.Ordinal)
        {
            ["Respond"] = new("Respond", "any", "text", "RESPOND-PROMPT", Role: "agent"),
            ["Verify"]  = new("Verify",  "any", "text", "VERIFY-PROMPT",  Role: "judge"),
        };

        var provider = new RepairLoopClient();
        var mission  = new MissionChatClient(repairAst, experts, new DirectExpertRunner(provider), fullConversation: true);

        // Continuation: agent answers → Verify FAILS → loop re-enters the agent segment,
        // which asks for another tool → the tool_use returns to the client.
        var response = await mission.GetResponseAsync(Continuation(), Tools());

        var call = Assert.Single(response.Messages.Single().Contents.OfType<FunctionCallContent>());
        Assert.Equal("toolu_repair_1", call.CallId);
        Assert.Equal(2, provider.AgentCalls);    // re-entered after the failed verification
        Assert.Equal(1, provider.VerifyCalls);
    }

    private sealed class RepairLoopClient : IChatClient
    {
        public int AgentCalls  { get; private set; }
        public int VerifyCalls { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            var system = messages.FirstOrDefault(m => m.Role == ChatRole.System)?.Text ?? "";

            if (system.Contains("VERIFY-PROMPT"))
            {
                VerifyCalls++;
                return Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant,
                    """{"text": "not grounded", "status": "fail", "reason": "answer lacks a source"}""")]));
            }

            AgentCalls++;
            var reply = AgentCalls == 1
                ? new ChatMessage(ChatRole.Assistant, "ungrounded answer")
                : new ChatMessage(ChatRole.Assistant,
                    [new FunctionCallContent("toolu_repair_1", "Read",
                        new Dictionary<string, object?> { ["file_path"] = "/tmp/source.txt" })]);
            return Task.FromResult(new ChatResponse([reply]));
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

    // ------------------------------------------------------------------
    // Conversation identity (P/F) sanity
    // ------------------------------------------------------------------
    [Fact]
    public void PrefixHash_IsStableAcrossToolContinuations_FullHashIsNot()
    {
        var turn = UserTurn();
        var continuation = Continuation();

        Assert.Equal(ConversationHash.Prefix(turn), ConversationHash.Prefix(continuation));
        Assert.NotEqual(ConversationHash.Full(turn), ConversationHash.Full(continuation));
    }

    // ------------------------------------------------------------------
    // Conversations as the wire would hand them over (BuildChatHistory shapes)
    // ------------------------------------------------------------------

    private static List<ChatMessage> UserTurn() =>
    [
        new(ChatRole.System, "client system prompt"),
        new(ChatRole.User, "read the probe file and tell me the magic word"),
    ];

    private static List<ChatMessage> Continuation() =>
    [
        new(ChatRole.System, "client system prompt"),
        new(ChatRole.User, "read the probe file and tell me the magic word"),
        new(ChatRole.Assistant,
            [new FunctionCallContent("toolu_seg_1", "Read", new Dictionary<string, object?> { ["file_path"] = "/tmp/x" })]),
        new(ChatRole.Tool,   // BuildChatHistory maps wire tool_results to Tool-role messages
            [new FunctionResultContent("toolu_seg_1", "the magic word is PLATYPUS")]),
    ];

    private static ChatOptions Tools() => new()
    {
        Tools = [AIFunctionFactory.Create((string file_path) => "", "Read", "Reads a file")],
    };

    // Scripted provider that attributes calls to segments via the expert system prompts.
    private sealed class SegmentAwareClient : IChatClient
    {
        public int EnrichCalls  { get; private set; }
        public int AgentCalls   { get; private set; }
        public int VerifyCalls  { get; private set; }
        public List<List<ChatMessage>> AgentMessages { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            var all    = messages.ToList();
            var system = all.FirstOrDefault(m => m.Role == ChatRole.System)?.Text ?? "";

            if (system.Contains("ENRICH-PROMPT")) { EnrichCalls++; return Envelope("enriched context"); }
            if (system.Contains("VERIFY-PROMPT")) { VerifyCalls++; return Envelope("verified: PLATYPUS"); }

            AgentCalls++;
            AgentMessages.Add(all);
            var hasToolResult = all.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Any();
            var reply = hasToolResult
                ? new ChatMessage(ChatRole.Assistant, "the magic word is PLATYPUS")
                : new ChatMessage(ChatRole.Assistant,
                    [new FunctionCallContent("toolu_seg_1", "Read",
                        new Dictionary<string, object?> { ["file_path"] = "/tmp/x" })]);
            return Task.FromResult(new ChatResponse([reply]));
        }

        private static Task<ChatResponse> Envelope(string text)
            => Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant,
                $$"""{"text": "{{text}}", "status": "pass", "reason": null}""")]));

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
