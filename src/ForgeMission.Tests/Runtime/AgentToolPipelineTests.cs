using System.Runtime.CompilerServices;
using System.Text.Json;
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
    // PipelineRunOptions.AllowMultipleToolCalls (Phase 43.16 Task 8b) — proves the closed
    // context-bag seam end-to-end: PipelineRunOptions -> PipelineRunner's context write ->
    // DirectExpertRunner's read -> the real ChatOptions the provider client receives.
    // ------------------------------------------------------------------
    [Fact]
    public async Task AllowMultipleToolCalls_False_IsAppliedToTheAgentExpertsChatOptions()
    {
        var client = new ScriptedPipelineClient(
            onToolCapableCall: _ => new ChatResponse([new ChatMessage(ChatRole.Assistant, "final answer")]));

        await new PipelineRunner(new DirectExpertRunner(client)).RunAsync(
            Ast, Experts(),
            new PipelineRunOptions("Task",
                new Dictionary<string, string> { ["goal"] = "read the probe file" },
                Tools: ClientTools(),
                AllowMultipleToolCalls: false));

        Assert.False(client.Calls[1].Options?.AllowMultipleToolCalls);
    }

    [Fact]
    public async Task AllowMultipleToolCalls_Default_LeavesTheAgentExpertsChatOptionsUnset()
    {
        var client = new ScriptedPipelineClient(
            onToolCapableCall: _ => new ChatResponse([new ChatMessage(ChatRole.Assistant, "final answer")]));
        await RunAsync(client); // AllowMultipleToolCalls left at its null default

        Assert.Null(client.Calls[1].Options?.AllowMultipleToolCalls);
    }

    [Fact]
    public async Task AllowMultipleToolCalls_False_IsAppliedOnTheStreamingPath()
    {
        var client = new ScriptedPipelineClient(
            onToolCapableCall: _ => new ChatResponse([new ChatMessage(ChatRole.Assistant, "final answer")]));

        // A non-null StepWriter forces DirectExpertRunner.StreamAsync instead of RunAsync
        // (PipelineRunner's streaming-trigger condition) — proves the seam on the streaming path
        // through the public API only, without reaching into the internal instruction key.
        await new PipelineRunner(new DirectExpertRunner(client)).RunAsync(
            Ast, Experts(),
            new PipelineRunOptions("Task",
                new Dictionary<string, string> { ["goal"] = "read the probe file" },
                StepWriter: TextWriter.Null,
                Tools: ClientTools(),
                AllowMultipleToolCalls: false));

        Assert.False(client.Calls[1].Options?.AllowMultipleToolCalls);
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
    // Phase 46 Task A: root-scoped generic nested continuation
    // ------------------------------------------------------------------
    [Fact]
    public async Task RootScopedToolPause_ResumesNestedAgent_WithExactProviderToolResult()
    {
        var ast = MclParser.Parse("""
            mission Child = { Enrich -> Respond -> Verify }
            mission Root = { Child }
            """);
        var experts = new Dictionary<string, ExpertDefinition>(StringComparer.Ordinal)
        {
            ["Enrich"] = new("Enrich", "any", "text", "You enrich."),
            ["Respond"] = new("Respond", "any", "text", "You respond.", Role: "agent"),
            ["Verify"] = new("Verify", "any", "text", "You verify."),
        };
        var client = new ContinuationClient();
        var runner = new PipelineRunner(new DirectExpertRunner(client));

        var paused = await runner.RunAsync(ast, experts,
            new PipelineRunOptions("Root", new Dictionary<string, string> { ["apiKey"] = "never-persist" }, RootTools: ClientTools()));

        var pause = Assert.IsType<PipelineToolPause>(paused.Pause);
        Assert.Equal(["Root", "Child"], pause.MissionPath);
        Assert.Equal("Respond", pause.ExpertName);
        Assert.Equal("Read", pause.ToolCall.Name);
        Assert.Equal(1, pause.Continuation.FormatVersion);
        Assert.DoesNotContain("dispatcher", pause.Continuation.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", pause.Continuation.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("never-persist", pause.Continuation.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("You respond.", pause.Continuation.Payload, StringComparison.Ordinal);

        var reloadedContinuation = JsonSerializer.Deserialize<PipelineContinuation>(
            JsonSerializer.Serialize(pause.Continuation))!;
        var completed = await new PipelineRunner(new DirectExpertRunner(client)).ResumeAsync(ast, experts,
            new PipelineResumeRequest(reloadedContinuation,
                new PipelineToolResult(pause.ToolCall.CallId, PipelineToolResultStatus.Succeeded, "probe content")),
            new PipelineRunOptions("ignored"));

        Assert.Equal(MissionStatus.Pass, completed.Status);
        Assert.Null(completed.Pause);
        Assert.Equal(4, client.Calls.Count); // Enrich ran only before the pause; then resumed agent + Verify
        var result = Assert.Single(client.Calls[2].Messages.SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>());
        Assert.Equal(pause.ToolCall.CallId, result.CallId);
        Assert.Equal("probe content", result.Result);
    }

    [Fact]
    public async Task RootScopedToolPause_RejectsUnsupportedAndDuplicateCallsWithoutProviderResume()
    {
        var ast = MclParser.Parse("mission Root = { Respond }");
        var experts = new Dictionary<string, ExpertDefinition>(StringComparer.Ordinal)
        {
            ["Respond"] = new("Respond", "any", "text", "You respond.", Role: "agent"),
        };
        var client = new ContinuationClient();
        var runner = new PipelineRunner(new DirectExpertRunner(client));
        var paused = await runner.RunAsync(ast, experts,
            new PipelineRunOptions("Root", RootTools: ClientTools()));
        var pause = Assert.IsType<PipelineToolPause>(paused.Pause);

        var invalid = await runner.ResumeAsync(ast, experts,
            new PipelineResumeRequest(pause.Continuation with { FormatVersion = 99 },
                new PipelineToolResult(pause.ToolCall.CallId, PipelineToolResultStatus.Succeeded)),
            new PipelineRunOptions("ignored"));
        Assert.Equal(PipelineFailure.InvalidContinuation, invalid.Failure);
        Assert.Single(client.Calls);

        var completed = await runner.ResumeAsync(ast, experts,
            new PipelineResumeRequest(pause.Continuation,
                new PipelineToolResult(pause.ToolCall.CallId, PipelineToolResultStatus.Denied, "not allowed")),
            new PipelineRunOptions("ignored"));
        Assert.Equal(MissionStatus.Pass, completed.Status);

        var duplicate = await runner.ResumeAsync(ast, experts,
            new PipelineResumeRequest(pause.Continuation,
                new PipelineToolResult(pause.ToolCall.CallId, PipelineToolResultStatus.Succeeded)),
            new PipelineRunOptions("ignored"));
        Assert.Equal(PipelineFailure.DuplicateContinuation, duplicate.Failure);
        Assert.Equal(2, client.Calls.Count);
    }

    [Fact]
    public async Task RootScopedParallelAgents_PauseAndResumeInSourceOrder()
    {
        var ast = MclParser.Parse("""
            mission Left = { LeftAgent }
            mission Right = { RightAgent }
            mission Root = {
                parallel {
                    Left
                    Right
                }
            }
            """);
        var experts = new Dictionary<string, ExpertDefinition>(StringComparer.Ordinal)
        {
            ["LeftAgent"] = new("LeftAgent", "any", "text", "Left agent.", Role: "agent"),
            ["RightAgent"] = new("RightAgent", "any", "text", "Right agent.", Role: "agent"),
        };
        var client = new ContinuationClient();
        var runner = new PipelineRunner(new DirectExpertRunner(client));

        var first = await runner.RunAsync(ast, experts, new PipelineRunOptions("Root", RootTools: ClientTools()));
        var firstPause = Assert.IsType<PipelineToolPause>(first.Pause);
        Assert.Equal("LeftAgent", firstPause.ExpertName);
        Assert.Single(client.Calls); // Right must not reach its provider before Left resumes.

        var second = await runner.ResumeAsync(ast, experts,
            new PipelineResumeRequest(firstPause.Continuation,
                new PipelineToolResult(firstPause.ToolCall.CallId, PipelineToolResultStatus.Succeeded, "left")),
            new PipelineRunOptions("ignored"));
        var secondPause = Assert.IsType<PipelineToolPause>(second.Pause);
        Assert.Equal("RightAgent", secondPause.ExpertName);
        Assert.Equal(3, client.Calls.Count); // initial+continued Left, then initial Right

        var late = await runner.ResumeAsync(ast, experts,
            new PipelineResumeRequest(firstPause.Continuation,
                new PipelineToolResult(firstPause.ToolCall.CallId, PipelineToolResultStatus.Succeeded, "late")),
            new PipelineRunOptions("ignored"));
        Assert.Equal(PipelineFailure.LateContinuation, late.Failure);
        Assert.Equal(3, client.Calls.Count);

        var completed = await new PipelineRunner(new DirectExpertRunner(client)).ResumeAsync(ast, experts,
            new PipelineResumeRequest(secondPause.Continuation,
                new PipelineToolResult(secondPause.ToolCall.CallId, PipelineToolResultStatus.Succeeded, "right")),
            new PipelineRunOptions("ignored"));
        Assert.Equal(MissionStatus.Pass, completed.Status);
    }

    [Fact]
    public async Task RootScopedToolPause_RejectsUnsupportedAndMultipleCalls_BeforeResume()
    {
        var ast = MclParser.Parse("mission Root = { Respond }");
        var experts = new Dictionary<string, ExpertDefinition>(StringComparer.Ordinal)
        { ["Respond"] = new("Respond", "any", "text", "Respond.", Role: "agent") };

        var unsupportedClient = new ContinuationClient(_ => ToolReply("Other"));
        var unsupported = await new PipelineRunner(new DirectExpertRunner(unsupportedClient)).RunAsync(ast, experts,
            new PipelineRunOptions("Root", RootTools: ClientTools()));
        Assert.Equal(PipelineFailure.UnsupportedTool, unsupported.Failure);
        Assert.Single(unsupportedClient.Calls);

        var multipleClient = new ContinuationClient(_ => new ChatResponse([new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent("one", "Read", new Dictionary<string, object?>()),
             new FunctionCallContent("two", "Read", new Dictionary<string, object?>())])]));
        var multiple = await new PipelineRunner(new DirectExpertRunner(multipleClient)).RunAsync(ast, experts,
            new PipelineRunOptions("Root", RootTools: ClientTools()));
        Assert.Equal(PipelineFailure.MultipleOutstandingTools, multiple.Failure);
        Assert.Single(multipleClient.Calls);
    }

    [Fact]
    public async Task RootScopedToolPause_InvalidContinuations_DoNotInvokeProvider()
    {
        var ast = MclParser.Parse("mission Root = { Respond }");
        var experts = new Dictionary<string, ExpertDefinition>(StringComparer.Ordinal)
        { ["Respond"] = new("Respond", "any", "text", "Respond.", Role: "agent") };
        var client = new ContinuationClient();
        var pause = Assert.IsType<PipelineToolPause>((await new PipelineRunner(new DirectExpertRunner(client)).RunAsync(ast, experts,
            new PipelineRunOptions("Root", RootTools: ClientTools()))).Pause);

        foreach (var continuation in new[]
        {
            new PipelineContinuation(1, "{"),
            pause.Continuation with { FormatVersion = 42 },
            pause.Continuation with { Payload = pause.Continuation.Payload.Replace("Read", "Other", StringComparison.Ordinal) },
        })
        {
            var result = await new PipelineRunner(new DirectExpertRunner(client)).ResumeAsync(ast, experts,
                new PipelineResumeRequest(continuation, new PipelineToolResult(pause.ToolCall.CallId, PipelineToolResultStatus.Succeeded)),
                new PipelineRunOptions("ignored"));
            Assert.Equal(PipelineFailure.InvalidContinuation, result.Failure);
        }

        var wrongCall = await new PipelineRunner(new DirectExpertRunner(client)).ResumeAsync(ast, experts,
            new PipelineResumeRequest(pause.Continuation, new PipelineToolResult("wrong", PipelineToolResultStatus.Succeeded)),
            new PipelineRunOptions("ignored"));
        Assert.Equal(PipelineFailure.InvalidContinuation, wrongCall.Failure);

        var changedExperts = new Dictionary<string, ExpertDefinition>(experts, StringComparer.Ordinal)
        { ["Respond"] = experts["Respond"] with { SystemPrompt = "Changed definition." } };
        var wrongDefinition = await new PipelineRunner(new DirectExpertRunner(client)).ResumeAsync(ast, changedExperts,
            new PipelineResumeRequest(pause.Continuation, new PipelineToolResult(pause.ToolCall.CallId, PipelineToolResultStatus.Succeeded)),
            new PipelineRunOptions("ignored"));
        Assert.Equal(PipelineFailure.InvalidContinuation, wrongDefinition.Failure);
        Assert.Single(client.Calls);
    }

    [Theory]
    [InlineData(PipelineToolResultStatus.Denied)]
    [InlineData(PipelineToolResultStatus.Cancelled)]
    [InlineData(PipelineToolResultStatus.Failed)]
    public async Task RootScopedToolPause_DeliversNonSuccessResultsAsProviderErrors(PipelineToolResultStatus status)
    {
        var ast = MclParser.Parse("mission Root = { Respond }");
        var experts = new Dictionary<string, ExpertDefinition>(StringComparer.Ordinal)
        { ["Respond"] = new("Respond", "any", "text", "Respond.", Role: "agent") };
        var client = new ContinuationClient();
        var pause = Assert.IsType<PipelineToolPause>((await new PipelineRunner(new DirectExpertRunner(client)).RunAsync(ast, experts,
            new PipelineRunOptions("Root", RootTools: ClientTools()))).Pause);
        var completed = await new PipelineRunner(new DirectExpertRunner(client)).ResumeAsync(ast, experts,
            new PipelineResumeRequest(pause.Continuation, new PipelineToolResult(pause.ToolCall.CallId, status, "detail")),
            new PipelineRunOptions("ignored"));
        Assert.Equal(MissionStatus.Pass, completed.Status);
        var error = Assert.Single(client.Calls[1].Messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>());
        Assert.Contains(status.ToString(), error.Result?.ToString());
    }

    [Fact]
    public async Task RootScopedToolPause_ProviderFailureAndRootCancellation_AreExplicit()
    {
        var ast = MclParser.Parse("mission Root = { Respond }");
        var experts = new Dictionary<string, ExpertDefinition>(StringComparer.Ordinal)
        { ["Respond"] = new("Respond", "any", "text", "Respond.", Role: "agent") };
        var client = new ContinuationClient();
        var pause = Assert.IsType<PipelineToolPause>((await new PipelineRunner(new DirectExpertRunner(client)).RunAsync(ast, experts,
            new PipelineRunOptions("Root", RootTools: ClientTools()))).Pause);
        client.ThrowOnContinuation = true;
        var failed = await new PipelineRunner(new DirectExpertRunner(client)).ResumeAsync(ast, experts,
            new PipelineResumeRequest(pause.Continuation, new PipelineToolResult(pause.ToolCall.CallId, PipelineToolResultStatus.Succeeded)),
            new PipelineRunOptions("ignored"));
        Assert.Equal(PipelineFailure.ProviderFailed, failed.Failure);

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => new PipelineRunner(new DirectExpertRunner(new ContinuationClient())).RunAsync(
            ast, experts, new PipelineRunOptions("Root", RootTools: ClientTools()), cancelled.Token));
    }

    [Fact]
    public async Task RootScopedIneligibleParallel_RemainsConcurrent()
    {
        var ast = MclParser.Parse("mission Root = { parallel { First Second } }");
        var experts = new Dictionary<string, ExpertDefinition>(StringComparer.Ordinal)
        {
            ["First"] = new("First", "any", "text", "First."),
            ["Second"] = new("Second", "any", "text", "Second."),
        };
        var runner = new DelayedRunner();
        var result = await new PipelineRunner(runner).RunAsync(ast, experts,
            new PipelineRunOptions("Root", RootTools: ClientTools()));
        Assert.Equal(MissionStatus.Pass, result.Status);
        Assert.Equal(2, runner.MaximumConcurrent);
    }

    [Fact]
    public async Task RootScopedToolPause_ReDerivesEnvironmentStepBindingWithoutPersistingItsValue()
    {
        const string variable = "MCLPHASE46ENVTEST";
        var previous = Environment.GetEnvironmentVariable(variable);
        try
        {
            Environment.SetEnvironmentVariable(variable, "first-secret");
            var ast = MclParser.Parse($"mission Root = {{ Respond(input: env(\"{variable}\")) }}");
            var experts = new Dictionary<string, ExpertDefinition>(StringComparer.Ordinal)
            { ["Respond"] = new("Respond", "any", "text", "input={{input}}", Role: "agent") };
            var client = new ContinuationClient();
            var pause = Assert.IsType<PipelineToolPause>((await new PipelineRunner(new DirectExpertRunner(client)).RunAsync(ast, experts,
                new PipelineRunOptions("Root", RootTools: ClientTools()))).Pause);
            Assert.DoesNotContain("first-secret", pause.Continuation.Payload, StringComparison.Ordinal);

            Environment.SetEnvironmentVariable(variable, "re-derived");
            await new PipelineRunner(new DirectExpertRunner(client)).ResumeAsync(ast, experts,
                new PipelineResumeRequest(pause.Continuation, new PipelineToolResult(pause.ToolCall.CallId, PipelineToolResultStatus.Succeeded)),
                new PipelineRunOptions("ignored"));
            Assert.Contains("input=re-derived", client.Calls[1].Messages[0].Text);
        }
        finally { Environment.SetEnvironmentVariable(variable, previous); }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static ChatResponse ToolCallReply(ChatOptions? _) => new(
        [new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent("toolu_pipeline_1", "Read",
                new Dictionary<string, object?> { ["file_path"] = "/tmp/probe.txt" })])]);

    private static ChatResponse ToolReply(string name) => new(
        [new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent("toolu_pipeline_1", name, new Dictionary<string, object?>())])]);

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

    private sealed class ContinuationClient(Func<ChatOptions?, ChatResponse>? firstToolReply = null) : IChatClient
    {
        public List<(IList<ChatMessage> Messages, ChatOptions? Options)> Calls { get; } = [];
        public bool ThrowOnContinuation { get; set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            var captured = messages.ToList();
            Calls.Add((captured, options));
            var hasResult = captured.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Any();
            if (hasResult && ThrowOnContinuation) throw new InvalidOperationException("provider unavailable");
            var reply = options?.Tools is { Count: > 0 } && !hasResult
                ? (firstToolReply?.Invoke(options) ?? ToolCallReply(options))
                : new ChatResponse([new ChatMessage(ChatRole.Assistant,
                    """{"text": "continued", "status": "pass", "reason": null}""")]);
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

    private sealed class DelayedRunner : IExpertRunner
    {
        private int _active;
        public int MaximumConcurrent { get; private set; }

        public async Task<StepEnvelope> RunAsync(ExpertDefinition expert, Dictionary<string, object> context, CancellationToken ct)
        {
            var active = Interlocked.Increment(ref _active);
            MaximumConcurrent = Math.Max(MaximumConcurrent, active);
            try { await Task.Delay(60, ct); return new StepEnvelope(expert.Name); }
            finally { Interlocked.Decrement(ref _active); }
        }

        public async IAsyncEnumerable<string> StreamAsync(ExpertDefinition expert, Dictionary<string, object> context,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return (await RunAsync(expert, context, ct)).Text;
        }
    }
}
