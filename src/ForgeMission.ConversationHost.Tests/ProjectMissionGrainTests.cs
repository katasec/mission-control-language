using System.Text.Json;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Tests;

/// <summary>
/// Grain-level integration tests for a Project's Mission container and its child Mission Runs
/// (43.21 task 1), against real Azurite.
///
/// The claim under test is narrow and load-bearing: whichever of the two allow-listed missions a
/// Project selects, the durable result is the SAME shape. Most of the assertions below are
/// therefore comparisons between a Janus run and a Naive run rather than two separately written
/// expectations, because two hand-written expectations are exactly how the two paths would drift
/// apart without anyone noticing.
///
/// The other theme is that nothing here is pinned to the container: it holds no mission and no
/// capabilities, which is what lets one Project alternate between the two.
/// </summary>
[Collection("Azurite")]
public class ProjectMissionGrainTests(AzuriteFixture fixture)
{
    private const string Janus = "Janus";
    private const string Naive = "Naive";
    private const string Goal = "Ship a todos API";

    private static ConversationAddress NewAddress() => new("dev", Guid.NewGuid());

    private static List<ConversationEvent> DeserializeEvents(ConversationEventBatch batch)
        => [.. batch.EventJson.Select(json =>
            JsonSerializer.Deserialize(json, ConversationContractsJsonContext.Default.ConversationEvent)!)];

    private static ConversationSnapshot DeserializeSnapshot(ConversationSnapshotResult result)
        => JsonSerializer.Deserialize(result.SnapshotJson, ConversationContractsJsonContext.Default.ConversationSnapshot)!;

    private static async Task<ConversationCommandAcceptance> CreateContainerAsync(
        IConversationGrain grain, Guid projectId, string goal = Goal)
    {
        var result = await grain.AcceptProjectMissionContainerCreateAsync(
            new ConversationProjectMissionCreateInput(
                ConversationDeterministicIds.ProjectMissionContainerCreate(projectId), projectId, goal));
        Assert.Equal(ConversationCommandOutcome.Accepted, result.Outcome);
        return result.Acceptance!;
    }

    private static Task<ConversationCommandOutcomeResult> StartRunAsync(
        IConversationGrain grain, Guid commandId, string mission, string input)
        => grain.AcceptProjectMissionRunAsync(
            new ConversationProjectMissionRunInput(commandId, mission, input));

    // ── Container: no event, no run, no pinned mission ─────────────────────────────

    [Fact]
    public async Task Create_PinsTheContainer_AppendsNoEvent_AndPinsNoMission()
    {
        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(NewAddress());
        var projectId = Guid.NewGuid();

        var acceptance = await CreateContainerAsync(grain, projectId);

        Assert.Equal(0, acceptance.AcceptedSequence);
        Assert.Null(acceptance.RunId);
        Assert.Empty((await grain.ReadAfterAsync(0)).EventJson);

        var snapshot = DeserializeSnapshot(await grain.GetSnapshotAsync());
        Assert.Equal(ConversationPurpose.ProjectMission, snapshot.Purpose);
        Assert.Equal(projectId, snapshot.ProjectId);
        // The distinguishing property: a container has NO mission, and says so as a null rather
        // than as an empty string a caller would have to interpret.
        Assert.Null(snapshot.MissionRef);
        Assert.Null(snapshot.ActiveRunId);
        Assert.Empty(host.Dispatcher.Sent);
    }

    [Fact]
    public async Task Create_IsIdempotentOnContent_AndRefusesADifferentProjectOrGoal()
    {
        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(NewAddress());
        var projectId = Guid.NewGuid();

        await CreateContainerAsync(grain, projectId);
        var retry = await CreateContainerAsync(grain, projectId);
        Assert.Equal(0, retry.AcceptedSequence);

        var differentGoal = await grain.AcceptProjectMissionContainerCreateAsync(
            new ConversationProjectMissionCreateInput(Guid.NewGuid(), projectId, "A different goal"));
        var differentProject = await grain.AcceptProjectMissionContainerCreateAsync(
            new ConversationProjectMissionCreateInput(Guid.NewGuid(), Guid.NewGuid(), Goal));

        Assert.Equal(ConversationCommandOutcome.Conflict, differentGoal.Outcome);
        Assert.Equal(ConversationCommandOutcome.Conflict, differentProject.Outcome);
        Assert.Empty((await grain.ReadAfterAsync(0)).EventJson);
    }

    [Fact]
    public async Task Create_RequiresACommandIdProjectIdAndGoal()
    {
        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(NewAddress());

        foreach (var input in new[]
                 {
                     new ConversationProjectMissionCreateInput(Guid.Empty, Guid.NewGuid(), Goal),
                     new ConversationProjectMissionCreateInput(Guid.NewGuid(), Guid.Empty, Goal),
                     new ConversationProjectMissionCreateInput(Guid.NewGuid(), Guid.NewGuid(), "   "),
                 })
        {
            var result = await grain.AcceptProjectMissionContainerCreateAsync(input);
            Assert.Equal(ConversationCommandOutcome.Invalid, result.Outcome);
        }

        Assert.Empty((await grain.ReadAfterAsync(0)).EventJson);
    }

    // ── The point of the task: one run shape for either mission ────────────────────

    [Theory]
    [InlineData(Janus)]
    [InlineData(Naive)]
    public async Task EitherMission_CreatesOneOrdinaryRunWithRunScopedEvents(string mission)
    {
        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(NewAddress());
        await CreateContainerAsync(grain, Guid.NewGuid());

        var accepted = await StartRunAsync(grain, Guid.NewGuid(), mission, "do the thing");

        Assert.Equal(ConversationCommandOutcome.Accepted, accepted.Outcome);
        var runId = Assert.NotNull(accepted.Acceptance!.RunId);

        var events = DeserializeEvents(await grain.ReadAfterAsync(0));
        Assert.Collection(events,
            user =>
            {
                Assert.Equal(ConversationEventKind.UserMessage, user.Kind);
                Assert.Equal(ConversationParticipant.User, user.Participant);
                Assert.Equal("do the thing", user.Text);
                Assert.Equal(runId, user.RunId);
            },
            queued =>
            {
                Assert.Equal(ConversationEventKind.RunStatus, queued.Kind);
                Assert.Equal(ConversationRunStatus.Queued, queued.RunStatus);
                Assert.Equal(runId, queued.RunId);
            });

        // One command dispatched to the Worker, naming the selected mission and carrying the
        // container's pinned goal — which no caller supplied.
        var dispatched = Assert.Single(host.Dispatcher.Sent).Command;
        Assert.Equal(mission, dispatched.MissionRef);
        Assert.Equal(ConversationCommandKind.StartMission, dispatched.Kind);
        Assert.Equal(runId, dispatched.RunId);
        Assert.Equal(Goal, dispatched.ProjectGoal);
        // No local tool authority, for EITHER mission. Opening or invoking a Project must not let
        // a default run probe the machine.
        Assert.Empty(dispatched.Capabilities);
    }

    /// <summary>
    /// The capability baseline, stated as its own test rather than left as one assertion inside a
    /// broader one: starting a Project Mission Run grants NO local tool authority, for either
    /// mission. This is the rule a live Janus run violated before the correction — it was handed
    /// the session's real capabilities and used them to run `ls /` on the machine.
    /// </summary>
    [Theory]
    [InlineData(Janus)]
    [InlineData(Naive)]
    public async Task NeitherMissionIsDeclaredAnyCapability(string mission)
    {
        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(NewAddress());
        await CreateContainerAsync(grain, Guid.NewGuid());

        await StartRunAsync(grain, Guid.NewGuid(), mission, "do the thing");

        Assert.Empty(Assert.Single(host.Dispatcher.Sent).Command.Capabilities);
    }

    /// <summary>The enforcement is the ABSENCE of a field, not a validation rule — a rule can be
    /// forgotten or bypassed by a direct Host caller, a missing member cannot. This asserts the
    /// shape of the two messages a caller could reach, so re-adding a capability field to either
    /// one fails here rather than silently restoring tool authority.</summary>
    [Fact]
    public void NoProjectMissionMessageCanCarryACapability()
    {
        Assert.Equal(
            ["ContainerId", "CommandId", "Mission", "Input"],
            typeof(StartProjectMissionRunRequest).GetProperties().Select(p => p.Name));

        Assert.Equal(
            ["CommandId", "Mission", "Input"],
            typeof(ConversationProjectMissionRunInput).GetProperties().Select(p => p.Name));
    }

    /// <summary>The equivalence asserted directly, rather than inferred from two separate
    /// expectations: the same submission under each mission differs ONLY in the mission name.</summary>
    [Fact]
    public async Task AJanusRunAndANaiveRun_AreIndistinguishableApartFromTheirMission()
    {
        await using var host = await fixture.StartHostAsync();

        var janus = await RunOnceAsync(Janus);
        var naive = await RunOnceAsync(Naive);

        Assert.Equal(
            janus.Events.Select(e => (e.Kind, e.Participant, e.Sequence, e.RunStatus, HasRun: e.RunId is not null)),
            naive.Events.Select(e => (e.Kind, e.Participant, e.Sequence, e.RunStatus, HasRun: e.RunId is not null)));

        Assert.Equal(janus.Status, naive.Status);
        Assert.Equal(janus.AcceptedSequence, naive.AcceptedSequence);
        Assert.Equal(janus.Command.Kind, naive.Command.Kind);
        Assert.Equal(janus.Command.Goal, naive.Command.Goal);
        Assert.Equal(janus.Command.ProjectGoal, naive.Command.ProjectGoal);
        Assert.NotEqual(janus.Command.MissionRef, naive.Command.MissionRef);

        async Task<(List<ConversationEvent> Events, ConversationRunStatus Status, long AcceptedSequence, ConversationCommand Command)>
            RunOnceAsync(string mission)
        {
            var grain = host.GetConversationGrain(NewAddress());
            await CreateContainerAsync(grain, Guid.NewGuid());
            var accepted = await StartRunAsync(grain, Guid.NewGuid(), mission, "do the thing");
            var command = host.Dispatcher.Sent.Last().Command;
            return (DeserializeEvents(await grain.ReadAfterAsync(0)),
                accepted.Acceptance!.Status, accepted.Acceptance.AcceptedSequence, command);
        }
    }

    /// <summary>The container pins nothing, so a Project can alternate. This is the case the
    /// existing follow-up path structurally cannot serve, which is why this task exists.</summary>
    [Fact]
    public async Task OneContainer_RunsBothMissionsInTurn()
    {
        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(NewAddress());
        await CreateContainerAsync(grain, Guid.NewGuid());

        var first = await StartRunAsync(grain, Guid.NewGuid(), Janus, "first");
        await CompleteRunAsync(grain, first.Acceptance!.RunId!.Value);
        var second = await StartRunAsync(grain, Guid.NewGuid(), Naive, "second");

        Assert.Equal(ConversationCommandOutcome.Accepted, second.Outcome);
        Assert.NotEqual(first.Acceptance.RunId, second.Acceptance!.RunId);
        Assert.Equal([Janus, Naive], host.Dispatcher.Sent.Select(sent => sent.Command.MissionRef));

        // Still no pinned mission after two runs of two different missions.
        Assert.Null(DeserializeSnapshot(await grain.GetSnapshotAsync()).MissionRef);
    }

    // ── Idempotency and conflict ───────────────────────────────────────────────────

    [Fact]
    public async Task AnEqualRetry_ReturnsTheOriginalRun_AndStartsNoSecondOne()
    {
        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(NewAddress());
        await CreateContainerAsync(grain, Guid.NewGuid());
        var commandId = Guid.NewGuid();

        var first = await StartRunAsync(grain, commandId, Janus, "do the thing");
        var retry = await StartRunAsync(grain, commandId, Janus, "do the thing");

        Assert.Equal(ConversationCommandOutcome.Accepted, retry.Outcome);
        Assert.Equal(first.Acceptance!.RunId, retry.Acceptance!.RunId);
        Assert.Equal(2, DeserializeEvents(await grain.ReadAfterAsync(0)).Count);
        Assert.Single(host.Dispatcher.Sent);
    }

    [Theory]
    [InlineData(Janus, "different input")]
    [InlineData(Naive, "do the thing")]
    public async Task TheSameCommandIdWithChangedContent_IsAConflict_AndStartsNoSecondRun(
        string mission, string input)
    {
        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(NewAddress());
        await CreateContainerAsync(grain, Guid.NewGuid());
        var commandId = Guid.NewGuid();

        await StartRunAsync(grain, commandId, Janus, "do the thing");
        var changed = await StartRunAsync(grain, commandId, mission, input);

        Assert.Equal(ConversationCommandOutcome.Conflict, changed.Outcome);
        Assert.Equal(2, DeserializeEvents(await grain.ReadAfterAsync(0)).Count);
        Assert.Single(host.Dispatcher.Sent);
    }

    /// <summary>One active run per Project (43.21 MVP). Reported as its own outcome, not a generic
    /// conflict, because it is an ordinary state a surface explains rather than a bad request.</summary>
    [Fact]
    public async Task ASecondSubmissionWhileARunIsActive_IsRunAlreadyActive_AndAppendsNothing()
    {
        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(NewAddress());
        await CreateContainerAsync(grain, Guid.NewGuid());

        await StartRunAsync(grain, Guid.NewGuid(), Janus, "first");
        var second = await StartRunAsync(grain, Guid.NewGuid(), Naive, "second");

        Assert.Equal(ConversationCommandOutcome.RunAlreadyActive, second.Outcome);
        Assert.Equal(2, DeserializeEvents(await grain.ReadAfterAsync(0)).Count);
        Assert.Single(host.Dispatcher.Sent);
    }

    // ── Refusals ───────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankInput_IsInvalid_AndCreatesNoRun(string input)
    {
        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(NewAddress());
        await CreateContainerAsync(grain, Guid.NewGuid());

        var result = await StartRunAsync(grain, Guid.NewGuid(), Janus, input);

        Assert.Equal(ConversationCommandOutcome.Invalid, result.Outcome);
        Assert.Empty((await grain.ReadAfterAsync(0)).EventJson);
        Assert.Empty(host.Dispatcher.Sent);
    }

    [Fact]
    public async Task ARunStartAgainstAConversationThatIsNotAContainer_IsAConflict()
    {
        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(NewAddress());

        // A never-created conversation is not a container either: purpose defaults to MissionRun.
        var uncreated = await StartRunAsync(grain, Guid.NewGuid(), Janus, "do the thing");

        Assert.Equal(ConversationCommandOutcome.Conflict, uncreated.Outcome);
        Assert.Empty((await grain.ReadAfterAsync(0)).EventJson);
    }

    [Fact]
    public async Task AContainer_RefusesTheLegacyControlMessagePath()
    {
        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(NewAddress());
        await CreateContainerAsync(grain, Guid.NewGuid());

        var control = await grain.AcceptControlMessageAsync(
            new ConversationControlMessageInput(Guid.NewGuid(), "narrow the scope"));

        Assert.Equal(ConversationCommandOutcome.Conflict, control.Outcome);
        Assert.Empty((await grain.ReadAfterAsync(0)).EventJson);
    }

    [Fact]
    public async Task AContainerCreate_RefusesAConversationAlreadyPinnedToAnotherPurpose()
    {
        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(NewAddress());
        var projectId = Guid.NewGuid();

        await grain.AcceptControlCreateAsync(
            new ConversationControlCreateInput(
                ConversationDeterministicIds.ProjectControlCreate(projectId), projectId, Goal));

        var asContainer = await grain.AcceptProjectMissionContainerCreateAsync(
            new ConversationProjectMissionCreateInput(Guid.NewGuid(), projectId, Goal));

        Assert.Equal(ConversationCommandOutcome.Conflict, asContainer.Outcome);
        Assert.Equal(ConversationPurpose.ProjectControl,
            DeserializeSnapshot(await grain.GetSnapshotAsync()).Purpose);
    }

    // Ends the active run so a second one may start, using the same terminal fact the Worker
    // publishes. Written through the real progress path rather than by poking checkpoint state,
    // so "the run finished" means what it means everywhere else.
    private static async Task CompleteRunAsync(IConversationGrain grain, Guid runId)
    {
        var snapshot = DeserializeSnapshot(await grain.GetSnapshotAsync());
        var progress = new ConversationProgress(
            Guid.NewGuid(), snapshot.ConversationId, runId, ConversationEventKind.RunStatus,
            ConversationParticipant.Forge, null, null, null, null, null, null, null,
            ConversationRunStatus.Completed, DateTimeOffset.UtcNow);

        var accepted = await grain.RecordProgressAsync(new ConversationProgressInput(
            JsonSerializer.Serialize(progress, ConversationContractsJsonContext.Default.ConversationProgress)));
        Assert.Equal(ConversationProgressOutcome.Appended, accepted.Outcome);
    }
}
