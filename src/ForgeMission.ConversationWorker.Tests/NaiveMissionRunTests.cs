using ForgeMission.ConversationWorker.Janus;
using ForgeMission.ConversationWorker.Messaging;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Manifest;
using ForgeMission.Core.Runtime;
using ForgeMission.Parser;

namespace ForgeMission.ConversationWorker.Tests;

/// <summary>
/// A Naive Mission Run in the Worker (43.21 task 1).
///
/// The contrast with <c>MissionControlTurnTests</c> is the whole subject. Both execute the SAME
/// one-expert asset, but a control turn publishes one fact and never a run status, while a Naive
/// RUN publishes a participant message and then a terminal run status — the same two-part shape a
/// Janus run ends with. That difference is the task: one mission asset, two callers, and only the
/// run-shaped one is reachable from the new contract.
/// </summary>
public class NaiveMissionRunTests
{
    private const string NaiveMissionSource = """
        mission Naive(projectGoal, task) = {
            Controller
        }
        """;

    private const string JanusMissionSource = """
        mission Negotiate(task) loop(5) = {
            Proposer using implementer
        }
        """;

    private static WorkerMissionResolver Resolver(IExpertRunner naiveRunner)
    {
        var naive = new WorkerMissionContext
        {
            Ast = MclParser.Parse(NaiveMissionSource),
            // No Role: "agent" — the packaged expert declares no agent role, which is half of how
            // tools are withheld (the other half is passing no Tools in the run options).
            Experts = new Dictionary<string, ExpertDefinition>(StringComparer.Ordinal)
            {
                ["Controller"] = new ExpertDefinition("Controller", "in", "out", "prompt", Role: ""),
            },
            Runners = new Dictionary<string, IExpertRunner>(StringComparer.Ordinal) { ["default"] = naiveRunner },
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
        return new WorkerMissionResolver(janus, naive);
    }

    // A Project Mission child command as ConversationGrain builds it: a real run id, the container's
    // pinned project goal, and no capabilities.
    private static ConversationCommand NaiveRunCommand(
        string input = "explain the tradeoff",
        string? projectGoal = "Ship a todos API",
        string mission = WorkerMissionResolver.NaiveRef) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ConversationCommandKind.StartMission,
            mission, input, [], null, projectGoal);

    private static Func<WorkerSessionState, CancellationToken, Task> SaveTo(List<WorkerSessionState> saved)
        => (state, _) => { saved.Add(state); return Task.CompletedTask; };

    private static Func<ConversationProgress, string, CancellationToken, Task> PublishTo(List<ConversationProgress> sink)
        => (progress, _, _) => { sink.Add(progress); return Task.CompletedTask; };

    private static IExpertRunner Answering(string answer) =>
        new FakeExpertRunner((_, _) => new StepEnvelope(answer, "pass"));

    private static IExpertRunner Failing(string reason) =>
        new FakeExpertRunner((_, _) => new StepEnvelope("", "fail", reason));

    [Fact]
    public async Task ANaiveRun_PublishesItsAnswerThenATerminalRunStatus()
    {
        var processor = new MissionCommandProcessor(Resolver(Answering("Pick the simpler one.")));
        var published = new List<ConversationProgress>();
        var command = NaiveRunCommand();

        var final = await processor.ProcessAsync(
            command, "dev", session: null, SaveTo([]), PublishTo(published), CancellationToken.None);

        Assert.Collection(published,
            answer =>
            {
                Assert.Equal(ConversationEventKind.ParticipantMessage, answer.Kind);
                // Labelled as the mission that produced it — never "forge", and never a Janus
                // participant it has nothing to do with.
                Assert.Equal(ConversationParticipant.Naive, answer.Participant);
                Assert.Equal("Pick the simpler one.", answer.Text);
                Assert.Equal(command.RunId, answer.RunId);
            },
            status =>
            {
                Assert.Equal(ConversationEventKind.RunStatus, status.Kind);
                Assert.Equal(ConversationRunStatus.Completed, status.RunStatus);
                Assert.Equal(command.RunId, status.RunId);
            });

        Assert.Equal(WorkerSessionPhase.Terminal, final.Phase);
    }

    /// <summary>Unlike the legacy control turn, every Naive fact belongs to a run. That is what
    /// makes the two selections indistinguishable downstream.</summary>
    [Fact]
    public async Task EveryNaiveFact_CarriesTheRunId()
    {
        var processor = new MissionCommandProcessor(Resolver(Answering("ok")));
        var published = new List<ConversationProgress>();
        var command = NaiveRunCommand();

        await processor.ProcessAsync(
            command, "dev", session: null, SaveTo([]), PublishTo(published), CancellationToken.None);

        Assert.All(published, fact => Assert.Equal(command.RunId, fact.RunId));
    }

    [Fact]
    public async Task AFailedNaiveRun_PublishesAnErrorThenFailed_AndNoAnswer()
    {
        var processor = new MissionCommandProcessor(Resolver(Failing("the provider refused")));
        var published = new List<ConversationProgress>();

        await processor.ProcessAsync(
            NaiveRunCommand(), "dev", session: null, SaveTo([]), PublishTo(published), CancellationToken.None);

        Assert.Collection(published,
            error =>
            {
                Assert.Equal(ConversationEventKind.Error, error.Kind);
                Assert.Contains("the provider refused", error.Reason);
            },
            status =>
            {
                Assert.Equal(ConversationEventKind.RunStatus, status.Kind);
                Assert.Equal(ConversationRunStatus.Failed, status.RunStatus);
            });

        Assert.DoesNotContain(published, p => p.Kind == ConversationEventKind.ParticipantMessage);
    }

    [Fact]
    public async Task ANaiveRun_NeverRequestsATool()
    {
        var processor = new MissionCommandProcessor(Resolver(Answering("no tools here")));
        var published = new List<ConversationProgress>();

        await processor.ProcessAsync(
            NaiveRunCommand(), "dev", session: null, SaveTo([]), PublishTo(published), CancellationToken.None);

        Assert.DoesNotContain(published, p => p.Kind == ConversationEventKind.ToolRequested);
    }

    [Fact]
    public async Task TheProjectGoalAndInstruction_ReachTheMissionAsSeparateVariables()
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
            NaiveRunCommand(input: "explain the tradeoff", projectGoal: "Ship a todos API"),
            "dev", session: null, SaveTo([]), PublishTo([]), CancellationToken.None);

        Assert.Equal("Ship a todos API", Assert.Contains("projectGoal", seen)?.ToString());
        Assert.Equal("explain the tradeoff", Assert.Contains("task", seen)?.ToString());
    }

    /// <summary>A run's instruction is its own input, so unlike a control turn a blank Project goal
    /// is tolerated rather than fatal — a Project must still be able to run something.</summary>
    [Fact]
    public async Task ANaiveRun_StillCompletesWhenTheProjectGoalIsAbsent()
    {
        var processor = new MissionCommandProcessor(Resolver(Answering("still fine")));
        var published = new List<ConversationProgress>();

        await processor.ProcessAsync(
            NaiveRunCommand(projectGoal: null), "dev", session: null, SaveTo([]), PublishTo(published), CancellationToken.None);

        Assert.Contains(published, p => p.Kind == ConversationEventKind.ParticipantMessage);
        Assert.Contains(published, p => p.RunStatus == ConversationRunStatus.Completed);
    }

    // ── The closed catalog ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Janus", WorkerMissionKind.Janus)]
    [InlineData("Naive", WorkerMissionKind.Naive)]
    [InlineData("MissionControl", WorkerMissionKind.ProjectControl)]
    [InlineData("Default", WorkerMissionKind.Unsupported)]
    [InlineData("naive", WorkerMissionKind.Unsupported)]
    [InlineData("gpt-4o", WorkerMissionKind.Unsupported)]
    [InlineData("Controller", WorkerMissionKind.Unsupported)]
    [InlineData("../naive", WorkerMissionKind.Unsupported)]
    [InlineData("", WorkerMissionKind.Unsupported)]
    [InlineData(null, WorkerMissionKind.Unsupported)]
    public void TheCatalogIsClosed(string? missionRef, WorkerMissionKind expected) =>
        Assert.Equal(expected, Resolver(Answering("ok")).Resolve(missionRef));

    /// <summary>A mission outside the catalog is reported as a failed run rather than guessed at,
    /// and — crucially — is never silently executed as one of the two that do exist.</summary>
    [Fact]
    public async Task AMissionOutsideTheCatalog_FailsTheRunRatherThanExecutingAnything()
    {
        var executed = false;
        var runner = new FakeExpertRunner((_, _) => { executed = true; return new StepEnvelope("ok", "pass"); });
        var processor = new MissionCommandProcessor(Resolver(runner));
        var published = new List<ConversationProgress>();

        await processor.ProcessAsync(
            NaiveRunCommand(mission: "SomethingElse"), "dev", session: null, SaveTo([]), PublishTo(published),
            CancellationToken.None);

        Assert.False(executed);
        Assert.Contains(published, p => p.RunStatus == ConversationRunStatus.Failed);
        Assert.DoesNotContain(published, p => p.Kind == ConversationEventKind.ParticipantMessage);
    }
}
