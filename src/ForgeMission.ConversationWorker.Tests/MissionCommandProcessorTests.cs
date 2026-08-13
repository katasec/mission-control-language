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

    private static JanusMissionContext BuildMission(IExpertRunner runner)
    {
        var ast = MclParser.Parse(MissionSource);
        return new JanusMissionContext
        {
            Ast = ast,
            Experts = Experts,
            Runners = new Dictionary<string, IExpertRunner>(StringComparer.Ordinal) { ["implementer"] = runner },
            Execution = new ExecutionConfig(),
        };
    }

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
        var processor = new MissionCommandProcessor(mission);
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
        var processor = new MissionCommandProcessor(mission);
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
        var processor = new MissionCommandProcessor(mission);
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

    [Fact]
    public async Task MismatchedContinuation_DoesNothing_NoExecutorCall()
    {
        var mission = BuildMission(new ThrowingExpertRunner());
        var processor = new MissionCommandProcessor(mission);
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
        var processor = new MissionCommandProcessor(mission);
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
        var processor = new MissionCommandProcessor(mission);
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
        var processor = new MissionCommandProcessor(mission);
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
        var processor = new MissionCommandProcessor(mission);
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
}
