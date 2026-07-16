using System.Runtime.CompilerServices;
using ForgeMission.Core.Adapters;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Runtime;
using ForgeMission.Parser;
using Microsoft.Extensions.AI;

namespace ForgeMission.Tests.Runtime;

/// <summary>
/// Tool-capable agent expert in the pipeline (Phase 42.3 task 4). Client tools attach to the
/// `role: agent` expert's provider call ONLY; a tool call from it ends the run immediately
/// (post-agent steps wait for the final continuation); a text answer lets the pipeline
/// continue into verification as usual.
/// </summary>
public sealed class AgentToolPipelineTests
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
        ["Enrich"]  = new("Enrich",  "any", "text", "You enrich."),
        ["Respond"] = new("Respond", "any", "text", "You respond.", Role: "agent"),
        ["Verify"]  = new("Verify",  "any", "text", "You verify."),
    };

    private static List<AITool> ClientTools() =>
        [AIFunctionFactory.Create((string file_path) => "", "Read", "Reads a file")];

    // ------------------------------------------------------------------
    // Agent calls a tool → run ends at the agent segment, Verify never runs
    // ------------------------------------------------------------------
    [Fact]
    public async Task AgentToolCall_ShortCircuits_BeforePostAgentSteps()
    {
        var client = new ScriptedPipelineClient(onToolCapableCall: ToolCallReply);
        var result = await RunAsync(client);

        Assert.Equal(MissionStatus.Pass, result.Status);
        var call = Assert.Single(result.ToolCalls!);
        Assert.Equal("Read", call.Name);
        Assert.Equal("toolu_pipeline_1", call.CallId);

        // Enrich + Respond ran; Verify did NOT (post-agent waits for the final continuation).
        Assert.Equal(2, client.Calls.Count);
    }

    [Fact]
    public async Task ToolsAttach_OnlyToTheAgentExpertsCall()
    {
        var client = new ScriptedPipelineClient(onToolCapableCall: ToolCallReply);
        await RunAsync(client);

        Assert.Null(client.Calls[0].Options?.Tools);          // Enrich never sees client tools
        var agentTools = client.Calls[1].Options?.Tools;      // Respond does
        Assert.NotNull(agentTools);
        Assert.Equal("Read", Assert.Single(agentTools!).Name);
    }

    // ------------------------------------------------------------------
    // Agent answers in text → pipeline continues; Verify runs; no ToolCalls
    // ------------------------------------------------------------------
    [Fact]
    public async Task AgentTextAnswer_ContinuesIntoPostAgentSteps()
    {
        var client = new ScriptedPipelineClient(
            onToolCapableCall: _ => new ChatResponse([new ChatMessage(ChatRole.Assistant, "final answer")]));
        var result = await RunAsync(client);

        Assert.Equal(MissionStatus.Pass, result.Status);
        Assert.Null(result.ToolCalls);
        Assert.Equal(3, client.Calls.Count);                  // Enrich, Respond, Verify all ran
    }

    // ------------------------------------------------------------------
    // Through MissionChatClient: tool calls surface as FunctionCallContent on the reply
    // ------------------------------------------------------------------
    [Fact]
    public async Task MissionChatClient_SurfacesToolCalls_OnTheReplyMessage()
    {
        var client  = new ScriptedPipelineClient(onToolCapableCall: ToolCallReply);
        var mission = new MissionChatClient(Ast, Experts(), new DirectExpertRunner(client), fullConversation: true);

        var response = await mission.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "read the probe file")],
            new ChatOptions { Tools = ClientTools() });

        var call = Assert.Single(response.Messages.Single().Contents.OfType<FunctionCallContent>());
        Assert.Equal("Read", call.Name);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static ChatResponse ToolCallReply(ChatOptions? _) => new(
        [new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent("toolu_pipeline_1", "Read",
                new Dictionary<string, object?> { ["file_path"] = "/tmp/probe.txt" })])]);

    private static async Task<MissionResult> RunAsync(ScriptedPipelineClient client)
        => await new PipelineRunner(new DirectExpertRunner(client)).RunAsync(
            Ast, Experts(),
            new PipelineRunOptions("Task",
                new Dictionary<string, string> { ["goal"] = "read the probe file" },
                Tools: ClientTools()));

    // Envelope JSON for ordinary experts; delegates to the script when tools are attached.
    private sealed class ScriptedPipelineClient(Func<ChatOptions?, ChatResponse> onToolCapableCall) : IChatClient
    {
        public List<(IList<ChatMessage> Messages, ChatOptions? Options)> Calls { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            Calls.Add((messages.ToList(), options));

            var reply = options?.Tools is { Count: > 0 }
                ? onToolCapableCall(options)
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
