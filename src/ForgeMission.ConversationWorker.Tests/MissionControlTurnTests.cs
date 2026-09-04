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
/// The zero-tool Project-control turn (43.20 task 2), driven through the same
/// <see cref="MissionCommandProcessor"/> and a real <see cref="PipelineRunner"/> over a scripted
/// runner. Proves the three properties the control contract turns on: exactly one fact per
/// outcome, never a <c>RunStatus</c>, and a Project goal that reaches the mission from the command
/// rather than from anything a caller supplied.
/// </summary>
public class MissionControlTurnTests
{
    // The Naive asset now serves both the legacy control turn and a Naive run (43.21 task 1), so
    // the fixture declares the mission under its real name.
    private const string ControlMissionSource = """
        mission Naive(projectGoal, task) = {
            Controller
        }
        """;

    private const string JanusMissionSource = """
        mission Negotiate(task) loop(5) = {
            Proposer using implementer
        }
        """;

    private static readonly Dictionary<string, ExpertDefinition> ControlExperts = new(StringComparer.Ordinal)
    {
        // No Role: "agent" — the packaged expert declares no agent role, which is half of how tools
        // are withheld (the other half is passing no Tools in the run options).
        ["Controller"] = new ExpertDefinition("Controller", "in", "out", "prompt", Role: ""),
    };

    private static WorkerMissionResolver Resolver(IExpertRunner controlRunner)
    {
        var control = new WorkerMissionContext
        {
            Ast = MclParser.Parse(ControlMissionSource),
            Experts = ControlExperts,
            Runners = new Dictionary<string, IExpertRunner>(StringComparer.Ordinal) { ["default"] = controlRunner },
            Execution = new ExecutionConfig(),
        };
        var janus = new WorkerMissionContext
        {
            Ast = MclParser.Parse(JanusMissionSource),
            Experts = new Dictionary<string, ExpertDefinition>(StringComparer.Ordinal)
            {
                ["Proposer"] = new ExpertDefinition("Proposer", "in", "out", "prompt", Role: ""),
            },
            Runners = new Dictionary<string, IExpertRunner>(StringComparer.Ordinal) { ["implementer"] = new ThrowingExpertRunner() },
            Execution = new ExecutionConfig(),
        };
        return new WorkerMissionResolver(janus, control);
    }

    private static ConversationCommand ControlCommand(
        Guid conversationId, Guid? commandId = null, string text = "narrow the scope",
        string? projectGoal = "Ship a todos API",
        ConversationCommandKind kind = ConversationCommandKind.StartMission) =>
        new(commandId ?? Guid.NewGuid(), conversationId, null, kind,
            WorkerMissionResolver.LegacyProjectControlRef, text, [], null, projectGoal);

    private static Func<WorkerSessionState, CancellationToken, Task> SaveTo(List<WorkerSessionState> saved)
        => (state, _) => { saved.Add(state); return Task.CompletedTask; };

    private static Func<ConversationProgress, string, CancellationToken, Task> PublishTo(List<ConversationProgress> sink)
        => (progress, _, _) => { sink.Add(progress); return Task.CompletedTask; };

    private static IExpertRunner Answering(string answer) =>
        new FakeExpertRunner((_, _) => new StepEnvelope(answer, "pass"));

    private static IExpertRunner Failing(string reason) =>
        new FakeExpertRunner((_, _) => new StepEnvelope("", "fail", reason));

    // ── 1. One fact per outcome, never a RunStatus ──────────────────────────────────

    [Fact]
    public async Task AControlTurn_PublishesExactlyOneParticipantMessage_AndNoRunStatusOrTool()
    {
        var processor = new MissionCommandProcessor(Resolver(Answering("What would done look like?")));
        var published = new List<ConversationProgress>();
        var saved = new List<WorkerSessionState>();

        var final = await processor.ProcessAsync(
            ControlCommand(Guid.NewGuid()), "dev", session: null, SaveTo(saved), PublishTo(published), CancellationToken.None);

        var fact = Assert.Single(published);
        Assert.Equal(ConversationEventKind.ParticipantMessage, fact.Kind);
        Assert.Equal(ConversationParticipant.MissionControl, fact.Participant);
        Assert.Equal("What would done look like?", fact.Text);
        Assert.Null(fact.RunId);

        Assert.DoesNotContain(published, p => p.Kind == ConversationEventKind.RunStatus);
        Assert.DoesNotContain(published, p => p.Kind == ConversationEventKind.ToolRequested);
        Assert.Equal(WorkerSessionPhase.Terminal, final.Phase);
        Assert.Null(final.RunId);
    }

    [Fact]
    public async Task AFailedControlTurn_PublishesExactlyOneError_AndNoRunStatus()
    {
        var processor = new MissionCommandProcessor(Resolver(Failing("the provider refused")));
        var published = new List<ConversationProgress>();

        var final = await processor.ProcessAsync(
            ControlCommand(Guid.NewGuid()), "dev", session: null, SaveTo([]), PublishTo(published), CancellationToken.None);

        var fact = Assert.Single(published);
        Assert.Equal(ConversationEventKind.Error, fact.Kind);
        Assert.Equal(ConversationParticipant.Forge, fact.Participant);
        Assert.Contains("the provider refused", fact.Reason);
        Assert.DoesNotContain(published, p => p.Kind == ConversationEventKind.RunStatus);
        Assert.Equal(WorkerSessionPhase.Terminal, final.Phase);
    }

    // ── 2. ProjectGoal reaches the mission, and a blank one is named ────────────────

    [Fact]
    public async Task TheProjectGoalAndTurnText_ReachTheMissionAsSeparateVariables()
    {
        var seen = new Dictionary<string, object>();
        var runner = new FakeExpertRunner((_, context) =>
        {
            foreach (var (key, value) in context)
                seen[key] = value;
            return new StepEnvelope("ok", "pass");
        });
        var processor = new MissionCommandProcessor(Resolver(runner));

        await processor.ProcessAsync(
            ControlCommand(Guid.NewGuid(), text: "narrow the scope", projectGoal: "Ship a todos API"),
            "dev", session: null, SaveTo([]), PublishTo([]), CancellationToken.None);

        Assert.Equal("Ship a todos API", Assert.Contains("projectGoal", seen)?.ToString());
        Assert.Equal("narrow the scope", Assert.Contains("task", seen)?.ToString());
    }

    // The grain is ProjectGoal's only writer and always sets a pinned non-empty value, so a blank
    // one is a corrupted command — named as such rather than substituted with an empty string the
    // mission would then silently reason over.
    [Fact]
    public async Task AControlCommandWithNoProjectGoal_IsNamedAsAnError_AndNeverRunsTheMission()
    {
        var processor = new MissionCommandProcessor(Resolver(new ThrowingExpertRunner()));
        var published = new List<ConversationProgress>();

        await processor.ProcessAsync(
            ControlCommand(Guid.NewGuid(), projectGoal: null), "dev", session: null,
            SaveTo([]), PublishTo(published), CancellationToken.None);

        var fact = Assert.Single(published);
        Assert.Equal(ConversationEventKind.Error, fact.Kind);
        Assert.Contains("no Project goal", fact.Reason);
    }

    // ── 3. Purpose-aware redelivery: an Error, never a RunStatus ────────────────────

    [Fact]
    public async Task AnInterruptedControlTurn_ReportsOneTruthfulError_AndNeverARunStatus()
    {
        var processor = new MissionCommandProcessor(Resolver(new ThrowingExpertRunner()));
        var published = new List<ConversationProgress>();
        var command = ControlCommand(Guid.NewGuid());

        // The session the redelivery finds: the same command, still marked ExecutingProvider,
        // meaning the process died with no chance to run its own failure handler.
        var interrupted = new WorkerSessionState(
            command.CommandId, null, WorkerSessionPhase.ExecutingProvider, 0, null, null, null);

        var final = await processor.ProcessAsync(
            command, "dev", interrupted, SaveTo([]), PublishTo(published), CancellationToken.None);

        var fact = Assert.Single(published);
        Assert.Equal(ConversationEventKind.Error, fact.Kind);
        Assert.Equal(ConversationParticipant.Forge, fact.Participant);
        Assert.Null(fact.RunStatus);
        Assert.Null(fact.RunId);
        // Truthful: it says the turn did not complete, and claims no stop, cancellation or rollback.
        Assert.Contains("interrupted before it completed", fact.Reason);
        Assert.DoesNotContain("stopped", fact.Reason!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rolled back", fact.Reason!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(WorkerSessionPhase.Terminal, final.Phase);
    }

    // The Janus half of the same branch is unchanged — the purpose decides the fact, and only for
    // a control command does it change.
    [Fact]
    public async Task AnInterruptedJanusRun_StillReportsRunStatusInterrupted()
    {
        var processor = new MissionCommandProcessor(Resolver(new ThrowingExpertRunner()));
        var published = new List<ConversationProgress>();
        var runId = Guid.NewGuid();
        var command = new ConversationCommand(
            Guid.NewGuid(), Guid.NewGuid(), runId, ConversationCommandKind.StartMission,
            WorkerMissionResolver.JanusRef, "do the work", [], null);
        var interrupted = new WorkerSessionState(
            command.CommandId, runId, WorkerSessionPhase.ExecutingProvider, 0, null, null, null);

        await processor.ProcessAsync(
            command, "dev", interrupted, SaveTo([]), PublishTo(published), CancellationToken.None);

        var fact = Assert.Single(published);
        Assert.Equal(ConversationEventKind.RunStatus, fact.Kind);
        Assert.Equal(ConversationRunStatus.Interrupted, fact.RunStatus);
        Assert.Equal(runId, fact.RunId);
    }

    // ── 4. Nullable WorkerSessionState.RunId — persistence and recovery ─────────────

    [Fact]
    public void AControlSessionStateRoundTripsWithANullRunId_AndALegacyNonNullOneStillLoads()
    {
        var control = new WorkerSessionState(Guid.NewGuid(), null, WorkerSessionPhase.ExecutingProvider, 0, null, null, null);

        var json = JsonSerializer.Serialize(control, WorkerSessionStateJsonContext.Default.WorkerSessionState);
        var restored = JsonSerializer.Deserialize(json, WorkerSessionStateJsonContext.Default.WorkerSessionState);

        Assert.Null(restored!.RunId);
        Assert.Equal(control.CurrentCommandId, restored.CurrentCommandId);

        // A session persisted before RunId became nullable carries a real GUID and still loads.
        var legacy = JsonSerializer.Deserialize(
            $$"""
            {"CurrentCommandId":"{{Guid.Empty}}","RunId":"{{Guid.Empty}}","Phase":1,
             "NextProgressOrdinal":3,"PendingProgressJson":null,"ApprovedPlan":"a plan","OutstandingTool":null}
            """,
            WorkerSessionStateJsonContext.Default.WorkerSessionState);
        Assert.Equal(Guid.Empty, legacy!.RunId);
        Assert.Equal(3, legacy.NextProgressOrdinal);
        Assert.Equal("a plan", legacy.ApprovedPlan);
    }

    // A crash between persisting a pending fact and its confirmed send resends the identical fact —
    // with a null RunId intact — under its already-assigned deterministic ID.
    [Fact]
    public async Task APendingControlFact_IsResentVerbatimAfterReload()
    {
        var processor = new MissionCommandProcessor(Resolver(Answering("ok")));
        var published = new List<ConversationProgress>();
        var command = ControlCommand(Guid.NewGuid());
        var pending = new ConversationProgress(
            ConversationDeterministicIds.Progress(command.CommandId, 0), command.ConversationId, null,
            ConversationEventKind.ParticipantMessage, ConversationParticipant.MissionControl,
            null, "an earlier answer", null, null, null, null, null, null, DateTimeOffset.UtcNow);

        var reloaded = new WorkerSessionState(
            command.CommandId, null, WorkerSessionPhase.ExecutingProvider, 0,
            JsonSerializer.Serialize(pending, ConversationContractsJsonContext.Default.ConversationProgress), null, null);

        await processor.ProcessAsync(
            command, "dev", reloaded, SaveTo([]), PublishTo(published), CancellationToken.None);

        var resent = published[0];
        Assert.Equal(pending.EventId, resent.EventId);
        Assert.Equal("an earlier answer", resent.Text);
        Assert.Null(resent.RunId);
    }

    // ── 5. A continuation can never reach the control path ─────────────────────────

    [Fact]
    public async Task AContinuationNamingMissionControl_IsRefused_AndNeverRunsJanusContinuation()
    {
        var processor = new MissionCommandProcessor(Resolver(new ThrowingExpertRunner()));
        var published = new List<ConversationProgress>();
        var command = ControlCommand(Guid.NewGuid(), kind: ConversationCommandKind.ContinueAfterTool);

        await processor.ProcessAsync(
            command, "dev", session: null, SaveTo([]), PublishTo(published), CancellationToken.None);

        var fact = Assert.Single(published);
        Assert.Equal(ConversationEventKind.Error, fact.Kind);
        Assert.Contains("no tool hand-off to continue", fact.Reason);
        Assert.DoesNotContain(published, p => p.Kind == ConversationEventKind.RunStatus);
    }
}

/// <summary>The named resolver itself: two fixed keys and one unsupported case.</summary>
public class WorkerMissionResolverTests
{
    private static WorkerMissionResolver Resolver()
    {
        var context = new WorkerMissionContext
        {
            Ast = MclParser.Parse("mission Noop(task) = {\n    Controller\n}"),
            Experts = new Dictionary<string, ExpertDefinition>(StringComparer.Ordinal),
            Runners = new Dictionary<string, IExpertRunner>(StringComparer.Ordinal),
            Execution = new ExecutionConfig(),
        };
        return new WorkerMissionResolver(context, context);
    }

    [Theory]
    [InlineData("Janus", WorkerMissionKind.Janus)]
    [InlineData("MissionControl", WorkerMissionKind.ProjectControl)]
    [InlineData("janus", WorkerMissionKind.Unsupported)]
    [InlineData("SomethingElse", WorkerMissionKind.Unsupported)]
    [InlineData("", WorkerMissionKind.Unsupported)]
    [InlineData(null, WorkerMissionKind.Unsupported)]
    public void Resolve_MapsOnlyTheTwoPackagedMissions(string? missionRef, WorkerMissionKind expected)
        => Assert.Equal(expected, Resolver().Resolve(missionRef));
}

/// <summary>
/// Loads the REAL checked-in Naive mission asset from disk (43.20 task 2, renamed by 43.21
/// task 1). This is what
/// proves the packaged mission is actually loadable — its mission/expert name pair, its generated
/// mcl.lock, and its provider profile — rather than only exercising a hand-built context.
/// </summary>
public class MissionControlMissionAssetTests
{
    private static string MissionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "ForgeMission.slnx")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "missions", "naive");
    }

    [Fact]
    public void ThePackagedMissionControlMission_LoadsWithOneZeroToolExpert()
    {
        Environment.SetEnvironmentVariable("MCL_API_KEY", "test-key-not-used-offline");

        var mission = WorkerMissionLoader.Load(MissionDirectory());

        var expert = Assert.Single(mission.Experts);
        Assert.Equal("Controller", expert.Key);
        // No agent role: half of how tools are withheld. The other half is the run options passing
        // no Tools, asserted in MissionControlMissionExecutor's own behaviour above.
        Assert.NotEqual("agent", expert.Value.Role);
        // The mission and its expert are deliberately named differently, so the loader is never
        // asked to resolve a shared name.
        Assert.Contains("mission Naive(projectGoal, task)",
            File.ReadAllText(Path.Combine(MissionDirectory(), "mission.mcl")));
    }

    // The expert's prompt is the contract with the model: it must consume both mission variables
    // and must not present itself as able to act.
    [Fact]
    public void TheControllerPrompt_ConsumesBothVariables_AndDisclaimsTools()
    {
        var prompt = File.ReadAllText(Path.Combine(MissionDirectory(), "experts", "Controller", "expert.md"));

        Assert.Contains("{{projectGoal}}", prompt);
        Assert.Contains("{{task}}", prompt);
        Assert.Contains("You have no tools.", prompt);
        Assert.DoesNotContain("role: agent", prompt);
    }
}
