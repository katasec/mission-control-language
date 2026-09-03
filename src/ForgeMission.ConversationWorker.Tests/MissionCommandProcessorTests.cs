using System.Text.Json;
using ForgeMission.ConversationWorker.Janus;
using ForgeMission.ConversationWorker.Messaging;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Manifest;
using ForgeMission.Core.Runtime;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Parser;

namespace ForgeMission.ConversationWorker.Tests;

/// <summary>
/// Drives <see cref="MissionCommandProcessor"/> — the SDK-independent core of Worker command
/// handling — against a real <see cref="PipelineRunner"/> execution over a scripted
/// <see cref="FakeExpertRunner"/> (no network/provider call). In-memory session-state and
/// progress-publish delegates stand in for Service Bus, matching the spoke's "small in-memory
/// seam" verification approach (this is not an Azure-emulator claim). Covers verification items 4
/// and 5: ExecutingProvider redelivery reports one Interrupted fact without executor replay,
/// pending-progress resend uses the same ID, a non-empty ToolCalls result publishes no terminal
/// status, a matching continuation changes the current command and runs only Implementer, and a
/// mismatched/duplicate continuation or StartMission does nothing.
/// </summary>
public class MissionCommandProcessorTests
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

    private static readonly Dictionary<string, ExpertDefinition> Experts = new(StringComparer.Ordinal)
    {
        ["Proposer"] = new ExpertDefinition("Proposer", "in", "out", "prompt", Role: ""),
        ["Approver"] = new ExpertDefinition("Approver", "in", "out", "prompt", Role: "judge"),
        ["Implementer"] = new ExpertDefinition("Implementer", "in", "out", "prompt", Role: "agent"),
    };

    private static WorkerMissionContext BuildMission(IExpertRunner runner)
    {
        var ast = MclParser.Parse(MissionSource);
        return new WorkerMissionContext
        {
            Ast = ast,
            Experts = Experts,
            Runners = new Dictionary<string, IExpertRunner>(StringComparer.Ordinal) { ["implementer"] = runner },
            Execution = new ExecutionConfig(),
        };
    }

    // Wraps one Janus context in the named resolver the processor now takes. The MissionControl
    // slot is the SAME context here: every test in this file exercises a Janus MissionRef, so the
    // control slot is never resolved — a control turn is covered by its own dedicated tests, with a
    // real MissionControl mission.
    private static WorkerMissionResolver Resolver(WorkerMissionContext janus) => new(janus, janus);

    private static ConversationCommand StartCommand(Guid conversationId, Guid runId, Guid? commandId = null) => new(
        commandId ?? Guid.NewGuid(), conversationId, runId, ConversationCommandKind.StartMission, "Janus", "do the task", [], null);

    // Negotiate passes on the first attempt (Approver passes immediately); Implementer passes with
    // no tool call — a full, uninterrupted happy path.
    private static IExpertRunner HappyPathRunner() => new FakeExpertRunner((expert, _) => expert.Name switch
    {
        "Proposer" => new StepEnvelope("here's my proposal", "pass"),
        "Approver" => new StepEnvelope("the approved plan", "pass"),
        "Implementer" => new StepEnvelope("done, no tool needed", "pass"),
        _ => throw new InvalidOperationException($"Unexpected expert '{expert.Name}'."),
    });

    // Same Negotiate happy path, but Implementer asks for a tool on its first call.
    private static IExpertRunner ToolPauseRunner() => new FakeExpertRunner((expert, context) =>
    {
        if (expert.Name == "Proposer") return new StepEnvelope("here's my proposal", "pass");
        if (expert.Name == "Approver") return new StepEnvelope("the approved plan", "pass");

        context["tool_calls"] = new List<Microsoft.Extensions.AI.FunctionCallContent>
        {
            new("provider-call-1", "Read", new Dictionary<string, object?> { ["file_path"] = "a.txt" }),
        };
        return new StepEnvelope("requesting a tool", "pass");
    });

    private static (List<ConversationProgress> Published, Dictionary<Guid, WorkerSessionState> SessionByConversation) NewSinks()
        => ([], []);

    private static Func<WorkerSessionState, CancellationToken, Task> SaveTo(Dictionary<Guid, WorkerSessionState> store, Guid conversationId)
        => (state, _) => { store[conversationId] = state; return Task.CompletedTask; };

    private static Func<ConversationProgress, string, CancellationToken, Task> PublishTo(List<ConversationProgress> sink)
        => (progress, _, _) => { sink.Add(progress); return Task.CompletedTask; };

    [Fact]
    public async Task FreshStartMission_NoToolCall_PublishesFactsAndEndsTerminalCompleted()
    {
        var mission = BuildMission(HappyPathRunner());
        var processor = new MissionCommandProcessor(Resolver(mission));
        var (published, sessions) = NewSinks();
        var conversationId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var command = StartCommand(conversationId, runId);

        var final = await processor.ProcessAsync(
            command, "dev", session: null, SaveTo(sessions, conversationId), PublishTo(published), CancellationToken.None);

        Assert.Equal(WorkerSessionPhase.Terminal, final.Phase);
        Assert.Equal("the approved plan", final.ApprovedPlan);
        Assert.Contains(published, p => p.Kind == ConversationEventKind.RunStatus && p.RunStatus == ConversationRunStatus.Completed);
        Assert.DoesNotContain(published, p => p.Kind == ConversationEventKind.ToolRequested);
        // Every published fact's EventId is unique — no ordinal reused.
        Assert.Equal(published.Select(p => p.EventId).Distinct().Count(), published.Count);
    }

    [Fact]
    public async Task ToolPause_PublishesNoTerminalStatus_AndLeavesSessionWaitingForTool()
    {
        var mission = BuildMission(ToolPauseRunner());
        var processor = new MissionCommandProcessor(Resolver(mission));
        var (published, sessions) = NewSinks();
        var conversationId = Guid.NewGuid();
        var command = StartCommand(conversationId, Guid.NewGuid());

        var final = await processor.ProcessAsync(
            command, "dev", session: null, SaveTo(sessions, conversationId), PublishTo(published), CancellationToken.None);

        Assert.Equal(WorkerSessionPhase.WaitingForTool, final.Phase);
        Assert.NotNull(final.OutstandingTool);
        Assert.Equal("Read", final.OutstandingTool!.ToolName);
        Assert.DoesNotContain(published, p => p.Kind == ConversationEventKind.RunStatus);
        Assert.Contains(published, p => p.Kind == ConversationEventKind.ToolRequested);
    }

    [Fact]
    public async Task MatchingContinuation_ChangesCurrentCommand_RunsOnlyImplementer_ReachesTerminalCompleted()
    {
        // Arrange: Implementer's SECOND call (the continuation) passes with no further tool call.
        var callCount = 0;
        var runner = new FakeExpertRunner((expert, context) =>
        {
            if (expert.Name is "Proposer" or "Approver")
                throw new InvalidOperationException($"'{expert.Name}' must not run again during a continuation.");

            callCount++;
            return new StepEnvelope("done after tool", "pass");
        });
        var mission = BuildMission(runner);
        var processor = new MissionCommandProcessor(Resolver(mission));
        var (published, sessions) = NewSinks();
        var conversationId = Guid.NewGuid();
        var runId = Guid.NewGuid();

        var toolRequestId = Guid.NewGuid();
        var waitingSession = new WorkerSessionState(
            Guid.NewGuid(), runId, WorkerSessionPhase.WaitingForTool, 3, null, "the approved plan",
            new OutstandingToolCall(toolRequestId, "provider-call-1", "Read", JsonDocument.Parse("{}").RootElement));

        var continueCommand = new ConversationCommand(
            Guid.NewGuid(), conversationId, runId, ConversationCommandKind.ContinueAfterTool, "Janus", "do the task", [],
            new ConversationToolResult(toolRequestId, "file contents", IsError: false));

        var final = await processor.ProcessAsync(
            continueCommand, "dev", waitingSession, SaveTo(sessions, conversationId), PublishTo(published), CancellationToken.None);

        Assert.Equal(1, callCount);
        Assert.Equal(continueCommand.CommandId, final.CurrentCommandId);
        Assert.Equal(WorkerSessionPhase.Terminal, final.Phase);
        Assert.Contains(published, p => p.Kind == ConversationEventKind.RunStatus && p.RunStatus == ConversationRunStatus.Completed);
    }

    // A real multi-file plan (the exact 3-file rate-limiter shape from Task 8's failed live run)
    // driven through three sequential ContinueAfterTool rounds — Phase 43.16 Task 8b's proof that
    // the one-tool-call-per-turn contract actually completes a multi-file plan end to end, not
    // just that the constraint is set. This test does not exercise ChatOptions/DirectExpertRunner
    // (FakeExpertRunner never builds one) — see JanusMissionExecutorToolCallOptionsTests for that;
    // this proves persistence/ordering of the sequential hand-off through the Worker itself.
    [Fact]
    public async Task SequentialToolCalls_AcrossThreeContinuations_CompleteAThreeFilePlan()
    {
        var implementerCallCount = 0;
        var files = new[] { "rate_limiter.py", "server.py", "test_rate_limiter.py" };

        var runner = new FakeExpertRunner((expert, context) =>
        {
            if (expert.Name is "Proposer" or "Approver")
                return new StepEnvelope("the approved plan", "pass");

            if (implementerCallCount < files.Length)
            {
                var file = files[implementerCallCount];
                implementerCallCount++;
                context["tool_calls"] = new List<Microsoft.Extensions.AI.FunctionCallContent>
                {
                    new($"provider-call-{implementerCallCount}", "Write",
                        new Dictionary<string, object?> { ["file_path"] = file }),
                };
                return new StepEnvelope($"writing {file}", "pass");
            }

            implementerCallCount++;
            return new StepEnvelope("all three files written and verified", "pass");
        });

        var mission = BuildMission(runner);
        var processor = new MissionCommandProcessor(Resolver(mission));
        var (published, sessions) = NewSinks();
        var conversationId = Guid.NewGuid();
        var runId = Guid.NewGuid();

        var session = await processor.ProcessAsync(
            StartCommand(conversationId, runId), "dev", session: null,
            SaveTo(sessions, conversationId), PublishTo(published), CancellationToken.None);

        // Two more rounds after the first: submit the outstanding tool's result, which drives the
        // next tool call (or, on the third round, the final completion).
        for (var round = 0; round < 3; round++)
        {
            Assert.Equal(WorkerSessionPhase.WaitingForTool, session.Phase);
            var outstanding = session.OutstandingTool!;
            var continueCommand = new ConversationCommand(
                Guid.NewGuid(), conversationId, runId, ConversationCommandKind.ContinueAfterTool, "Janus",
                "do the task", [], new ConversationToolResult(outstanding.RequestId, "ok", IsError: false));

            session = await processor.ProcessAsync(
                continueCommand, "dev", session, SaveTo(sessions, conversationId), PublishTo(published), CancellationToken.None);
        }

        Assert.Equal(4, implementerCallCount); // 3 tool calls + 1 final no-tool completion
        Assert.Equal(WorkerSessionPhase.Terminal, session.Phase);
        Assert.Equal(files.Length, published.Count(p => p.Kind == ConversationEventKind.ToolRequested));
        Assert.Contains(published, p => p.Kind == ConversationEventKind.RunStatus && p.RunStatus == ConversationRunStatus.Completed);
        Assert.DoesNotContain(published, p => p.Kind == ConversationEventKind.Error);

        // In order: three ToolRequested facts, one per file, each naming that file's tool call.
        var toolRequests = published.Where(p => p.Kind == ConversationEventKind.ToolRequested).ToList();
        Assert.Equal(files.Length, toolRequests.Count);
        for (var i = 0; i < files.Length; i++)
            Assert.Equal(files[i], toolRequests[i].ToolRequest?.Arguments.GetProperty("file_path").GetString());
    }

    [Fact]
    public async Task MismatchedContinuation_DoesNothing_NoExecutorCall()
    {
        var mission = BuildMission(new ThrowingExpertRunner());
        var processor = new MissionCommandProcessor(Resolver(mission));
        var (published, sessions) = NewSinks();
        var conversationId = Guid.NewGuid();
        var runId = Guid.NewGuid();

        var waitingSession = new WorkerSessionState(
            Guid.NewGuid(), runId, WorkerSessionPhase.WaitingForTool, 1, null, "plan",
            new OutstandingToolCall(Guid.NewGuid(), "provider-call-1", "Read", JsonDocument.Parse("{}").RootElement));

        // Wrong RequestId — a mismatched tool result.
        var wrongResultCommand = new ConversationCommand(
            Guid.NewGuid(), conversationId, runId, ConversationCommandKind.ContinueAfterTool, "Janus", "do the task", [],
            new ConversationToolResult(Guid.NewGuid(), "content", IsError: false));

        var final = await processor.ProcessAsync(
            wrongResultCommand, "dev", waitingSession, SaveTo(sessions, conversationId), PublishTo(published), CancellationToken.None);

        Assert.Equal(waitingSession, final);
        Assert.Empty(published);
    }

    [Fact]
    public async Task DuplicateStartMission_WhileWaitingForTool_DoesNothing_NoExecutorCall()
    {
        var mission = BuildMission(new ThrowingExpertRunner());
        var processor = new MissionCommandProcessor(Resolver(mission));
        var (published, sessions) = NewSinks();
        var conversationId = Guid.NewGuid();
        var runId = Guid.NewGuid();

        var waitingSession = new WorkerSessionState(
            Guid.NewGuid(), runId, WorkerSessionPhase.WaitingForTool, 1, null, "plan",
            new OutstandingToolCall(Guid.NewGuid(), "provider-call-1", "Read", JsonDocument.Parse("{}").RootElement));

        var duplicateStart = StartCommand(conversationId, runId);

        var final = await processor.ProcessAsync(
            duplicateStart, "dev", waitingSession, SaveTo(sessions, conversationId), PublishTo(published), CancellationToken.None);

        Assert.Equal(waitingSession, final);
        Assert.Empty(published);
    }

    [Fact]
    public async Task RedeliveryWhileExecutingProvider_EmitsOneInterrupted_NoExecutorReplay()
    {
        var mission = BuildMission(new ThrowingExpertRunner());
        var processor = new MissionCommandProcessor(Resolver(mission));
        var (published, sessions) = NewSinks();
        var conversationId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var command = StartCommand(conversationId, runId);

        var executingSession = new WorkerSessionState(command.CommandId, runId, WorkerSessionPhase.ExecutingProvider, 2, null, null, null);

        var final = await processor.ProcessAsync(
            command, "dev", executingSession, SaveTo(sessions, conversationId), PublishTo(published), CancellationToken.None);

        var fact = Assert.Single(published);
        Assert.Equal(ConversationEventKind.RunStatus, fact.Kind);
        Assert.Equal(ConversationRunStatus.Interrupted, fact.RunStatus);
        Assert.Equal(WorkerSessionPhase.Terminal, final.Phase);
    }

    [Fact]
    public async Task RedeliveryWhileTerminal_IsPlainNoOp()
    {
        var mission = BuildMission(new ThrowingExpertRunner());
        var processor = new MissionCommandProcessor(Resolver(mission));
        var (published, sessions) = NewSinks();
        var conversationId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var command = StartCommand(conversationId, runId);

        var terminalSession = new WorkerSessionState(command.CommandId, runId, WorkerSessionPhase.Terminal, 5, null, "plan", null);

        var final = await processor.ProcessAsync(
            command, "dev", terminalSession, SaveTo(sessions, conversationId), PublishTo(published), CancellationToken.None);

        Assert.Equal(terminalSession, final);
        Assert.Empty(published);
    }

    [Fact]
    public async Task PendingProgressFromPriorCrash_ResendsBeforeAnythingElse_ThenClears()
    {
        var mission = BuildMission(new ThrowingExpertRunner());
        var processor = new MissionCommandProcessor(Resolver(mission));
        var (published, sessions) = NewSinks();
        var conversationId = Guid.NewGuid();
        var runId = Guid.NewGuid();

        var pendingEventId = Guid.NewGuid();
        var pendingProgress = new ConversationProgress(
            pendingEventId, conversationId, runId, ConversationEventKind.RunStatus, ConversationParticipant.Forge,
            null, null, null, null, null, null, null, ConversationRunStatus.Interrupted, DateTimeOffset.UtcNow);
        var pendingJson = JsonSerializer.Serialize(pendingProgress, ConversationContractsJsonContext.Default.ConversationProgress);

        // WaitingForTool, with a fact that was persisted-but-never-confirmed-sent — the exact
        // shape a mid-send crash leaves behind. A duplicate StartMission while a run is active is
        // a no-op on its own, so it drives this without touching the executor.
        var crashedSession = new WorkerSessionState(
            Guid.NewGuid(), runId, WorkerSessionPhase.WaitingForTool, 4, pendingJson, "plan",
            new OutstandingToolCall(Guid.NewGuid(), "provider-call-1", "Read", JsonDocument.Parse("{}").RootElement));
        var command = StartCommand(conversationId, runId);

        var final = await processor.ProcessAsync(
            command, "dev", crashedSession, SaveTo(sessions, conversationId), PublishTo(published), CancellationToken.None);

        var resent = Assert.Single(published);
        Assert.Equal(pendingEventId, resent.EventId);
        Assert.Null(final.PendingProgressJson);
        Assert.Equal(5, final.NextProgressOrdinal);
    }

    // ── Review round 2, correction 1: ToolRequested ordering and ID ──────────────

    [Fact]
    public async Task ToolRequested_PersistsWaitingForToolStateBeforeThePublisherEverSeesTheFact()
    {
        var mission = BuildMission(ToolPauseRunner());
        var processor = new MissionCommandProcessor(Resolver(mission));
        var conversationId = Guid.NewGuid();
        var command = StartCommand(conversationId, Guid.NewGuid());

        var callIndex = 0;
        int? firstWaitingForToolSaveIndex = null;
        int? toolRequestedPublishIndex = null;

        Task Save(WorkerSessionState state, CancellationToken _)
        {
            if (firstWaitingForToolSaveIndex is null && state.Phase == WorkerSessionPhase.WaitingForTool && state.OutstandingTool is not null)
                firstWaitingForToolSaveIndex = callIndex;
            callIndex++;
            return Task.CompletedTask;
        }

        Task Publish(ConversationProgress progress, string _, CancellationToken __)
        {
            if (progress.Kind == ConversationEventKind.ToolRequested)
                toolRequestedPublishIndex ??= callIndex;
            callIndex++;
            return Task.CompletedTask;
        }

        await processor.ProcessAsync(command, "dev", session: null, Save, Publish, CancellationToken.None);

        Assert.NotNull(firstWaitingForToolSaveIndex);
        Assert.NotNull(toolRequestedPublishIndex);
        Assert.True(firstWaitingForToolSaveIndex < toolRequestedPublishIndex,
            $"WaitingForTool state must be saved (call #{firstWaitingForToolSaveIndex}) before the " +
            $"publisher ever sees the ToolRequested fact (call #{toolRequestedPublishIndex}).");
    }

    [Fact]
    public async Task ToolRequested_UsesTheCurrentNonZeroOrdinal_NotAFixedZero()
    {
        // Proposer + Approver each publish ParticipantStarted/ParticipantMessage/Approval facts
        // before Implementer ever runs, so the ordinal in effect when Implementer's tool request
        // fires is well past 0.
        var mission = BuildMission(ToolPauseRunner());
        var processor = new MissionCommandProcessor(Resolver(mission));
        var (published, sessions) = NewSinks();
        var conversationId = Guid.NewGuid();
        var command = StartCommand(conversationId, Guid.NewGuid());

        var final = await processor.ProcessAsync(
            command, "dev", session: null, SaveTo(sessions, conversationId), PublishTo(published), CancellationToken.None);

        var zeroOrdinalId = ConversationDeterministicIds.ToolRequest(command.CommandId, 0);
        Assert.NotEqual(zeroOrdinalId, final.OutstandingTool!.RequestId);

        var toolRequestedFact = Assert.Single(published, p => p.Kind == ConversationEventKind.ToolRequested);
        Assert.Equal(toolRequestedFact.ToolRequest!.RequestId, final.OutstandingTool.RequestId);

        // The ToolRequested fact's own EventId and its ToolRequest.RequestId are derived from the
        // same ordinal (Progress and ToolRequest share it — they name the same fact) — proved here
        // by reconstructing the ToolRequest ID from the ordinal implicit in the fact's own EventId
        // (Progress and ToolRequest use the same namespace-scoped Generate primitive, so equal
        // ordinal input to both always agrees) rather than assuming ordinal 0.
        var ordinal = published.IndexOf(toolRequestedFact);
        Assert.Equal(ConversationDeterministicIds.ToolRequest(command.CommandId, ordinal), final.OutstandingTool.RequestId);
    }

    // ── Review round 2, correction 2: known execution failures ───────────────────

    [Fact]
    public async Task ExecutorException_PublishesErrorThenFailed_NeverInterrupted()
    {
        var runner = new FakeExpertRunner((expert, _) => expert.Name switch
        {
            "Proposer" => new StepEnvelope("here's my proposal", "pass"),
            "Approver" => new StepEnvelope("the approved plan", "pass"),
            "Implementer" => throw new InvalidOperationException("simulated provider failure"),
            _ => throw new InvalidOperationException($"Unexpected expert '{expert.Name}'."),
        });
        var mission = BuildMission(runner);
        var processor = new MissionCommandProcessor(Resolver(mission));
        var (published, sessions) = NewSinks();
        var conversationId = Guid.NewGuid();
        var command = StartCommand(conversationId, Guid.NewGuid());

        var final = await processor.ProcessAsync(
            command, "dev", session: null, SaveTo(sessions, conversationId), PublishTo(published), CancellationToken.None);

        Assert.Equal(WorkerSessionPhase.Terminal, final.Phase);
        // PipelineRunner itself wraps the raw exception ("Step 'Implementer' failed: <message>")
        // before it reaches MissionCommandProcessor — Contains, not exact-match, keeps this test
        // decoupled from that wrapping format.
        Assert.Contains(published, p => p.Kind == ConversationEventKind.Error
            && p.Reason!.Contains("simulated provider failure", StringComparison.Ordinal));
        var runStatus = Assert.Single(published, p => p.Kind == ConversationEventKind.RunStatus);
        Assert.Equal(ConversationRunStatus.Failed, runStatus.RunStatus);
        Assert.DoesNotContain(published, p => p.RunStatus == ConversationRunStatus.Interrupted);
    }

    [Fact]
    public async Task CancelledOperation_PropagatesUncaught_LeavingExecutingProviderForRedeliveryToReportInterrupted()
    {
        using var cts = new CancellationTokenSource();
        var runner = new FakeExpertRunner((expert, _) =>
        {
            if (expert.Name == "Proposer")
            {
                cts.Cancel();
                cts.Token.ThrowIfCancellationRequested();
            }
            throw new InvalidOperationException($"Unexpected expert '{expert.Name}'.");
        });
        var mission = BuildMission(runner);
        var processor = new MissionCommandProcessor(Resolver(mission));
        var (published, sessions) = NewSinks();
        var conversationId = Guid.NewGuid();
        var command = StartCommand(conversationId, Guid.NewGuid());

        await Assert.ThrowsAsync<OperationCanceledException>(() => processor.ProcessAsync(
            command, "dev", session: null, SaveTo(sessions, conversationId), PublishTo(published), cts.Token));

        // Nothing durable resolved this as a known failure — a later redelivery is what reports
        // Interrupted, proving the exception was never caught and turned into Error/Failed here.
        Assert.DoesNotContain(published, p => p.Kind is ConversationEventKind.Error or ConversationEventKind.RunStatus);
    }

    // ── Review round 2, correction 4: mission admission ───────────────────────────

    [Fact]
    public async Task WrongMissionRef_FailsVisibly_NoExecutorCall()
    {
        var mission = BuildMission(new ThrowingExpertRunner());
        var processor = new MissionCommandProcessor(Resolver(mission));
        var (published, sessions) = NewSinks();
        var conversationId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var command = new ConversationCommand(
            Guid.NewGuid(), conversationId, runId, ConversationCommandKind.StartMission, "NotJanus", "goal", [], null);

        var final = await processor.ProcessAsync(
            command, "dev", session: null, SaveTo(sessions, conversationId), PublishTo(published), CancellationToken.None);

        Assert.Equal(WorkerSessionPhase.Terminal, final.Phase);
        Assert.Contains(published, p => p.Kind == ConversationEventKind.Error);
        Assert.Contains(published, p => p.Kind == ConversationEventKind.RunStatus && p.RunStatus == ConversationRunStatus.Failed);
    }

    // ── Final review: outbox failure boundary ─────────────────────────────────────
    // A failure from the publish delegate itself (a transient broker/store problem, not a Janus
    // execution failure) must never be reclassified into a synthetic Error/Failed fact — doing so
    // could overwrite the pending fact already durably persisted for the send that just failed.

    [Fact]
    public async Task PublishFailureAfterPendingPersisted_PropagatesUnsettled_RetainsOriginalPendingFact_NoSyntheticOverwrite()
    {
        var mission = BuildMission(HappyPathRunner());
        var processor = new MissionCommandProcessor(Resolver(mission));
        var conversationId = Guid.NewGuid();
        var command = StartCommand(conversationId, Guid.NewGuid());

        var sessions = new Dictionary<Guid, WorkerSessionState>();
        var published = new List<ConversationProgress>();

        Task Save(WorkerSessionState state, CancellationToken _)
        {
            sessions[conversationId] = state;
            return Task.CompletedTask;
        }

        // Fails the very first fact this run ever tries to send (Proposer's ParticipantStarted) —
        // by then PendingProgressJson for it has already been durably saved.
        Task Publish(ConversationProgress progress, string _, CancellationToken __)
        {
            if (progress.Kind == ConversationEventKind.ParticipantStarted)
                throw new InvalidOperationException("simulated publish failure");
            published.Add(progress);
            return Task.CompletedTask;
        }

        var thrown = await Assert.ThrowsAnyAsync<Exception>(
            () => processor.ProcessAsync(command, "dev", session: null, Save, Publish, CancellationToken.None));

        // The private marker exception preserves the original message — asserted without needing
        // to reference the (deliberately private) wrapper type.
        Assert.Equal("simulated publish failure", thrown.Message);

        // Nothing after the failed fact ever ran: no Error/Failed fact was synthesized, and the
        // failed fact itself never actually reached the publisher's success path.
        Assert.Empty(published);

        Assert.True(sessions.ContainsKey(conversationId));
        var finalState = sessions[conversationId];
        Assert.NotNull(finalState.PendingProgressJson);
        var pendingFact = JsonSerializer.Deserialize(
            finalState.PendingProgressJson!, ConversationContractsJsonContext.Default.ConversationProgress)!;
        Assert.Equal(ConversationEventKind.ParticipantStarted, pendingFact.Kind);
        Assert.Equal(ConversationParticipant.Proposer, pendingFact.Participant);
        Assert.Equal(0, finalState.NextProgressOrdinal); // never advanced past the failed fact.
        Assert.Equal(WorkerSessionPhase.ExecutingProvider, finalState.Phase); // never reached Terminal.
    }
}
