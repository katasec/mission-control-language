using System.Text.Json;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Tests;

/// <summary>
/// Grain-level integration tests for a Project's Mission Control conversation (43.20 task 2),
/// against real Azurite. Each test uses a fresh conversation and, where reactivation matters,
/// starts a second Host/Silo against the same Azurite endpoint so replay is proved from durable
/// state rather than from in-memory state.
///
/// The theme throughout: a control conversation reuses the existing sequence allocator, event
/// store, outbox and replay unchanged, while the grain refuses — structurally, not by convention —
/// to let a run, a run status, a tool request, or a caller-supplied project goal onto it.
/// </summary>
[Collection("Azurite")]
public class ConversationControlGrainTests(AzuriteFixture fixture)
{
    private static ConversationAddress NewAddress() => new("dev", Guid.NewGuid());

    private static List<ConversationEvent> DeserializeEvents(ConversationEventBatch batch)
        => [.. batch.EventJson.Select(json =>
            JsonSerializer.Deserialize(json, ConversationContractsJsonContext.Default.ConversationEvent)!)];

    private static ConversationSnapshot DeserializeSnapshot(ConversationSnapshotResult result)
        => JsonSerializer.Deserialize(result.SnapshotJson, ConversationContractsJsonContext.Default.ConversationSnapshot)!;

    private static async Task<ConversationCommandAcceptance> CreateControlAsync(
        IConversationGrain grain, Guid projectId, string goal = "Ship a todos API")
    {
        var result = await grain.AcceptControlCreateAsync(
            new ConversationControlCreateInput(ConversationDeterministicIds.ProjectControlCreate(projectId), projectId, goal));
        Assert.Equal(ConversationCommandOutcome.Accepted, result.Outcome);
        return result.Acceptance!;
    }

    // ── 1. Create: no event, no run, sequence 0, idempotent on content ──────────────

    [Fact]
    public async Task Create_PinsTheConversation_AppendsNoEvent_AndStartsNoRun()
    {
        await using var host = await fixture.StartHostAsync();
        var address = NewAddress();
        var grain = host.GetConversationGrain(address);

        var acceptance = await CreateControlAsync(grain, Guid.NewGuid());

        // A newly created control conversation is empty: its accepted sequence is 0 because create
        // appends nothing at all.
        Assert.Equal(0, acceptance.AcceptedSequence);
        Assert.Null(acceptance.RunId);
        Assert.Empty((await grain.ReadAfterAsync(0)).EventJson);

        var snapshot = DeserializeSnapshot(await grain.GetSnapshotAsync());
        Assert.Equal(ConversationPurpose.ProjectControl, snapshot.Purpose);
        Assert.Equal("MissionControl", snapshot.MissionRef);
        Assert.Null(snapshot.ActiveRunId);
        Assert.Equal(0, snapshot.LastSequence);
        Assert.Empty(host.Dispatcher.Sent);
    }

    [Fact]
    public async Task Create_ExactRetry_ReturnsTheSameAcceptance_AndStillNoEvents()
    {
        await using var host = await fixture.StartHostAsync();
        var address = NewAddress();
        var grain = host.GetConversationGrain(address);
        var projectId = Guid.NewGuid();

        var first = await CreateControlAsync(grain, projectId);
        var retry = await CreateControlAsync(grain, projectId);

        Assert.Equal(first.ConversationId, retry.ConversationId);
        Assert.Equal(first.AcceptedSequence, retry.AcceptedSequence);
        Assert.Empty((await grain.ReadAfterAsync(0)).EventJson);
    }

    [Fact]
    public async Task Create_WithADifferentProjectOrGoal_IsAConflict()
    {
        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(NewAddress());
        var projectId = Guid.NewGuid();
        await CreateControlAsync(grain, projectId, "Ship a todos API");

        var differentGoal = await grain.AcceptControlCreateAsync(
            new ConversationControlCreateInput(
                ConversationDeterministicIds.ProjectControlCreate(projectId), projectId, "Something else entirely"));
        var differentProject = await grain.AcceptControlCreateAsync(
            new ConversationControlCreateInput(Guid.NewGuid(), Guid.NewGuid(), "Ship a todos API"));

        Assert.Equal(ConversationCommandOutcome.Conflict, differentGoal.Outcome);
        Assert.Equal(ConversationCommandOutcome.Conflict, differentProject.Outcome);
    }

    [Fact]
    public async Task Create_WithABlankGoalOrEmptyProject_IsInvalid()
    {
        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(NewAddress());

        var blankGoal = await grain.AcceptControlCreateAsync(
            new ConversationControlCreateInput(Guid.NewGuid(), Guid.NewGuid(), "   "));
        var noProject = await grain.AcceptControlCreateAsync(
            new ConversationControlCreateInput(Guid.NewGuid(), Guid.Empty, "Ship a todos API"));

        Assert.Equal(ConversationCommandOutcome.Invalid, blankGoal.Outcome);
        Assert.Equal(ConversationCommandOutcome.Invalid, noProject.Outcome);
    }

    // ── 2. One control turn: exactly one event, one zero-tool command, no run ───────

    [Fact]
    public async Task ControlMessage_AppendsOneUserMessage_AndDispatchesOneZeroToolCommand()
    {
        await using var host = await fixture.StartHostAsync();
        var address = NewAddress();
        var grain = host.GetConversationGrain(address);
        await CreateControlAsync(grain, Guid.NewGuid());

        var commandId = Guid.NewGuid();
        var result = await grain.AcceptControlMessageAsync(new ConversationControlMessageInput(commandId, "narrow the scope"));

        Assert.Equal(ConversationCommandOutcome.Accepted, result.Outcome);
        Assert.Null(result.Acceptance!.RunId);
        Assert.Equal(1, result.Acceptance.AcceptedSequence);

        // EXACTLY ONE event — no paired RunStatus(Queued), because no run was begun.
        var stored = Assert.Single(DeserializeEvents(await grain.ReadAfterAsync(0)));
        Assert.Equal(ConversationEventKind.UserMessage, stored.Kind);
        Assert.Equal(ConversationParticipant.User, stored.Participant);
        Assert.Equal("narrow the scope", stored.Text);
        Assert.Null(stored.RunId);
        Assert.Equal(commandId, stored.EventId);

        var dispatched = Assert.Single(host.Dispatcher.Sent).Command;
        Assert.Equal("MissionControl", dispatched.MissionRef);
        Assert.Null(dispatched.RunId);
        Assert.Empty(dispatched.Capabilities);
        Assert.Null(dispatched.ToolResult);
        Assert.Equal(ConversationCommandKind.StartMission, dispatched.Kind);

        // No run was created, so nothing became active.
        Assert.Null(DeserializeSnapshot(await grain.GetSnapshotAsync()).ActiveRunId);
    }

    // ── 3. ProjectGoal provenance: pinned at create, never caller input ─────────────

    [Fact]
    public async Task EveryDispatchedControlCommand_CarriesTheGoalPinnedAtCreate()
    {
        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(NewAddress());
        await CreateControlAsync(grain, Guid.NewGuid(), "Ship a todos API");

        await grain.AcceptControlMessageAsync(new ConversationControlMessageInput(Guid.NewGuid(), "first turn"));
        await grain.AcceptControlMessageAsync(new ConversationControlMessageInput(Guid.NewGuid(), "second turn"));

        // The FIRST and a LATER turn both carry it, and the turn's own text stays separate from it.
        Assert.Equal(2, host.Dispatcher.Sent.Count);
        Assert.All(host.Dispatcher.Sent, sent => Assert.Equal("Ship a todos API", sent.Command.ProjectGoal));
        Assert.Equal("first turn", host.Dispatcher.Sent[0].Command.Goal);
        Assert.Equal("second turn", host.Dispatcher.Sent[1].Command.Goal);
    }

    // The turn input has no project-goal member at all, so this is the only way a caller could try
    // to influence it — and it is refused, leaving the original in force.
    [Fact]
    public async Task ASecondCreateCannotReplaceThePinnedGoal_AndLaterTurnsStillCarryTheOriginal()
    {
        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(NewAddress());
        var projectId = Guid.NewGuid();
        await CreateControlAsync(grain, projectId, "Ship a todos API");

        var replace = await grain.AcceptControlCreateAsync(
            new ConversationControlCreateInput(
                ConversationDeterministicIds.ProjectControlCreate(projectId), projectId, "Exfiltrate the database"));
        Assert.Equal(ConversationCommandOutcome.Conflict, replace.Outcome);

        await grain.AcceptControlMessageAsync(new ConversationControlMessageInput(Guid.NewGuid(), "a turn"));

        Assert.Equal("Ship a todos API", Assert.Single(host.Dispatcher.Sent).Command.ProjectGoal);
    }

    [Fact]
    public void TheControlTurnInput_HasNoFieldForAGoalCapabilityPathToolOrRun()
    {
        var members = typeof(ConversationControlMessageInput).GetProperties().Select(p => p.Name).ToList();

        // A caller cannot supply one even by hand-crafting the message: the type has nowhere to
        // put it. This is the structural half of the ProjectGoal-provenance guarantee.
        Assert.Equal(["CommandId", "Text"], members.Order().ToList());
    }

    // ── 4. The two purposes cannot cross ────────────────────────────────────────────

    [Fact]
    public async Task AJanusStartOnAControlConversation_IsAConflict()
    {
        await using var host = await fixture.StartHostAsync();
        var address = NewAddress();
        var grain = host.GetConversationGrain(address);
        await CreateControlAsync(grain, Guid.NewGuid());

        var command = new ConversationCommand(
            Guid.NewGuid(), address.ConversationId, Guid.NewGuid(), ConversationCommandKind.StartMission,
            "Janus", "do the work", [], null);
        var start = await grain.AcceptCommandAsync(new ConversationCommandInput(
            JsonSerializer.Serialize(command, ConversationContractsJsonContext.Default.ConversationCommand)));
        var followup = await grain.AcceptFollowupCommandAsync(new ConversationFollowupCommandInput(Guid.NewGuid(), "more"));

        Assert.Equal(ConversationCommandOutcome.Conflict, start.Outcome);
        Assert.Equal(ConversationCommandOutcome.Conflict, followup.Outcome);
        Assert.Empty(host.Dispatcher.Sent);
    }

    [Fact]
    public async Task AControlMessageOnAJanusConversation_IsAConflict()
    {
        await using var host = await fixture.StartHostAsync();
        var address = NewAddress();
        var grain = host.GetConversationGrain(address);

        var command = new ConversationCommand(
            Guid.NewGuid(), address.ConversationId, Guid.NewGuid(), ConversationCommandKind.StartMission,
            "Janus", "do the work", [], null);
        await grain.AcceptCommandAsync(new ConversationCommandInput(
            JsonSerializer.Serialize(command, ConversationContractsJsonContext.Default.ConversationCommand)));

        var control = await grain.AcceptControlMessageAsync(new ConversationControlMessageInput(Guid.NewGuid(), "refine"));

        Assert.Equal(ConversationCommandOutcome.Conflict, control.Outcome);
    }

    [Fact]
    public async Task AJanusCommandCarryingAProjectGoalOrNoRunId_IsInvalid()
    {
        await using var host = await fixture.StartHostAsync();
        var address = NewAddress();
        var grain = host.GetConversationGrain(address);

        var withGoal = new ConversationCommand(
            Guid.NewGuid(), address.ConversationId, Guid.NewGuid(), ConversationCommandKind.StartMission,
            "Janus", "do the work", [], null, ProjectGoal: "Ship a todos API");
        var withoutRun = new ConversationCommand(
            Guid.NewGuid(), address.ConversationId, null, ConversationCommandKind.StartMission,
            "Janus", "do the work", [], null);

        var goalResult = await grain.AcceptCommandAsync(new ConversationCommandInput(
            JsonSerializer.Serialize(withGoal, ConversationContractsJsonContext.Default.ConversationCommand)));
        var runResult = await grain.AcceptCommandAsync(new ConversationCommandInput(
            JsonSerializer.Serialize(withoutRun, ConversationContractsJsonContext.Default.ConversationCommand)));

        Assert.Equal(ConversationCommandOutcome.Invalid, goalResult.Outcome);
        Assert.Equal(ConversationCommandOutcome.Invalid, runResult.Outcome);
    }

    // ── 5. Duplicate control turns ──────────────────────────────────────────────────

    [Fact]
    public async Task DuplicateControlMessage_SameText_ReturnsTheOriginalAcceptance_AndAppendsNothingNew()
    {
        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(NewAddress());
        await CreateControlAsync(grain, Guid.NewGuid());

        var commandId = Guid.NewGuid();
        var first = await grain.AcceptControlMessageAsync(new ConversationControlMessageInput(commandId, "refine"));
        var retry = await grain.AcceptControlMessageAsync(new ConversationControlMessageInput(commandId, "refine"));

        Assert.Equal(ConversationCommandOutcome.Accepted, retry.Outcome);
        Assert.Equal(first.Acceptance!.AcceptedSequence, retry.Acceptance!.AcceptedSequence);
        Assert.Single(DeserializeEvents(await grain.ReadAfterAsync(0)));
    }

    [Fact]
    public async Task DuplicateControlMessage_DifferentText_IsAConflict()
    {
        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(NewAddress());
        await CreateControlAsync(grain, Guid.NewGuid());

        var commandId = Guid.NewGuid();
        await grain.AcceptControlMessageAsync(new ConversationControlMessageInput(commandId, "refine"));
        var changed = await grain.AcceptControlMessageAsync(new ConversationControlMessageInput(commandId, "something else"));

        Assert.Equal(ConversationCommandOutcome.Conflict, changed.Outcome);
        Assert.Single(DeserializeEvents(await grain.ReadAfterAsync(0)));
    }

    // ── 6. Control progress admission — the structural containment ──────────────────

    [Fact]
    public async Task AControlParticipantMessage_IsAccepted()
    {
        await using var host = await fixture.StartHostAsync();
        var address = NewAddress();
        var grain = host.GetConversationGrain(address);
        await CreateControlAsync(grain, Guid.NewGuid());
        await grain.AcceptControlMessageAsync(new ConversationControlMessageInput(Guid.NewGuid(), "refine"));

        var acceptance = await RecordAsync(grain, address, ConversationEventKind.ParticipantMessage,
            ConversationParticipant.MissionControl, text: "What would done look like?");

        Assert.Equal(ConversationProgressOutcome.Appended, acceptance.Outcome);
        var events = DeserializeEvents(await grain.ReadAfterAsync(0));
        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.Null(e.RunId));
        Assert.Equal(ConversationParticipant.MissionControl, events[1].Participant);
    }

    // A RunStatus is the one the Worker's own redelivery rule must never send. Rejecting it here
    // means a Worker regression fails loudly at the sequence allocator instead of appending a
    // meaningless run status to a control transcript.
    [Fact]
    public async Task AControlRunStatusToolOrNonNullRunFact_IsRejected()
    {
        await using var host = await fixture.StartHostAsync();
        var address = NewAddress();
        var grain = host.GetConversationGrain(address);
        await CreateControlAsync(grain, Guid.NewGuid());

        var runStatus = await RecordAsync(grain, address, ConversationEventKind.RunStatus,
            ConversationParticipant.Forge, runStatus: ConversationRunStatus.Interrupted);
        using var arguments = JsonDocument.Parse("""{"command":"ls"}""");
        var toolRequested = await RecordAsync(grain, address, ConversationEventKind.ToolRequested,
            ConversationParticipant.Implementer,
            toolRequest: new ConversationToolRequest(Guid.NewGuid(), "Bash", arguments.RootElement));
        var withRun = await RecordAsync(grain, address, ConversationEventKind.ParticipantMessage,
            ConversationParticipant.MissionControl, text: "hello", runId: Guid.NewGuid());
        var approval = await RecordAsync(grain, address, ConversationEventKind.Approval,
            ConversationParticipant.Approver, approval: new ConversationApproval(ConversationApprovalOutcome.Approved, null));

        Assert.Equal(ConversationProgressOutcome.Rejected, runStatus.Outcome);
        Assert.Equal(ConversationProgressOutcome.Rejected, toolRequested.Outcome);
        Assert.Equal(ConversationProgressOutcome.Rejected, withRun.Outcome);
        Assert.Equal(ConversationProgressOutcome.Rejected, approval.Outcome);
        Assert.Empty((await grain.ReadAfterAsync(0)).EventJson);
    }

    // ── 7. Durable replay across a fresh Host ───────────────────────────────────────

    [Fact]
    public async Task AFreshHost_ReactivatesTheControlConversation_AndReplaysItsEventsInOrder()
    {
        var address = NewAddress();
        var projectId = Guid.NewGuid();

        await using (var host1 = await fixture.StartHostAsync())
        {
            var grain = host1.GetConversationGrain(address);
            await CreateControlAsync(grain, projectId, "Ship a todos API");
            await grain.AcceptControlMessageAsync(new ConversationControlMessageInput(Guid.NewGuid(), "refine"));
            await RecordAsync(grain, address, ConversationEventKind.ParticipantMessage,
                ConversationParticipant.MissionControl, text: "What would done look like?");
        }

        await using var host2 = await fixture.StartHostAsync();
        var replayed = DeserializeEvents(await host2.GetConversationGrain(address).ReadAfterAsync(0));

        Assert.Equal(2, replayed.Count);
        Assert.Equal([1, 2], replayed.Select(e => e.Sequence));
        Assert.Equal(ConversationEventKind.UserMessage, replayed[0].Kind);
        Assert.Equal(ConversationEventKind.ParticipantMessage, replayed[1].Kind);
        Assert.All(replayed, e => Assert.Null(e.RunId));

        // The pinned purpose and goal survive reactivation, so a later turn still carries the goal.
        var snapshot = DeserializeSnapshot(await host2.GetConversationGrain(address).GetSnapshotAsync());
        Assert.Equal(ConversationPurpose.ProjectControl, snapshot.Purpose);
        await host2.GetConversationGrain(address).AcceptControlMessageAsync(
            new ConversationControlMessageInput(Guid.NewGuid(), "another turn"));
        Assert.Equal("Ship a todos API", host2.Dispatcher.Sent[^1].Command.ProjectGoal);
    }

    // ── 8. Outbox: a failed send is retried under the identical command ─────────────

    [Fact]
    public async Task AFailedControlDispatch_IsRepairedWithTheIdenticalCommand()
    {
        await using var host = await fixture.StartHostAsync();
        var address = NewAddress();
        var grain = host.GetConversationGrain(address);
        await CreateControlAsync(grain, Guid.NewGuid(), "Ship a todos API");

        var commandId = Guid.NewGuid();
        host.Dispatcher.FailNextSend = true;
        await Assert.ThrowsAnyAsync<Exception>(() =>
            grain.AcceptControlMessageAsync(new ConversationControlMessageInput(commandId, "refine")));

        // The retry re-derives the identical command through the existing pending-transition
        // outbox rather than minting a second one.
        var retry = await grain.AcceptControlMessageAsync(new ConversationControlMessageInput(commandId, "refine"));

        Assert.Equal(ConversationCommandOutcome.Accepted, retry.Outcome);
        var dispatched = Assert.Single(host.Dispatcher.Sent).Command;
        Assert.Equal(commandId, dispatched.CommandId);
        Assert.Equal("MissionControl", dispatched.MissionRef);
        Assert.Equal("Ship a todos API", dispatched.ProjectGoal);
        Assert.Single(DeserializeEvents(await grain.ReadAfterAsync(0)));
    }

    // ── 9. Message-shape validation, refused before any state is consulted ─────────

    [Fact]
    public async Task ControlMessage_WithWhitespaceOnlyTextOrEmptyCommandId_IsInvalid_AndAppendsNothing()
    {
        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(NewAddress());
        await CreateControlAsync(grain, Guid.NewGuid());

        var whitespace = await grain.AcceptControlMessageAsync(new ConversationControlMessageInput(Guid.NewGuid(), "   \t\n "));
        var empty = await grain.AcceptControlMessageAsync(new ConversationControlMessageInput(Guid.NewGuid(), ""));
        var noCommandId = await grain.AcceptControlMessageAsync(new ConversationControlMessageInput(Guid.Empty, "refine"));

        Assert.Equal(ConversationCommandOutcome.Invalid, whitespace.Outcome);
        Assert.Equal(ConversationCommandOutcome.Invalid, empty.Outcome);
        Assert.Equal(ConversationCommandOutcome.Invalid, noCommandId.Outcome);

        // "Rejected" has to mean no durable fact AND no dispatched side effect — not merely a
        // typed result with an append that happened anyway.
        Assert.Empty((await grain.ReadAfterAsync(0)).EventJson);
        Assert.Empty(host.Dispatcher.Sent);
    }

    // The shape check runs before the purpose check, so a malformed message is refused the same
    // way whatever conversation it names — and can never reach the idempotency row under
    // Guid.Empty, where two unrelated malformed commands would collide.
    [Fact]
    public async Task AMalformedControlMessage_IsRefusedEvenOnANonControlConversation()
    {
        await using var host = await fixture.StartHostAsync();
        var address = NewAddress();
        var grain = host.GetConversationGrain(address);

        var command = new ConversationCommand(
            Guid.NewGuid(), address.ConversationId, Guid.NewGuid(), ConversationCommandKind.StartMission,
            "Janus", "do the work", [], null);
        await grain.AcceptCommandAsync(new ConversationCommandInput(
            JsonSerializer.Serialize(command, ConversationContractsJsonContext.Default.ConversationCommand)));

        var result = await grain.AcceptControlMessageAsync(new ConversationControlMessageInput(Guid.Empty, "  "));

        Assert.Equal(ConversationCommandOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public async Task ControlCreate_WithAnEmptyCommandId_IsInvalid()
    {
        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(NewAddress());

        var result = await grain.AcceptControlCreateAsync(
            new ConversationControlCreateInput(Guid.Empty, Guid.NewGuid(), "Ship a todos API"));

        Assert.Equal(ConversationCommandOutcome.Invalid, result.Outcome);
    }

    private static Task<ConversationProgressAcceptance> RecordAsync(
        IConversationGrain grain,
        ConversationAddress address,
        ConversationEventKind kind,
        ConversationParticipant participant,
        string? text = null,
        ConversationApproval? approval = null,
        ConversationToolRequest? toolRequest = null,
        ConversationRunStatus? runStatus = null,
        Guid? runId = null)
    {
        var progress = new ConversationProgress(
            Guid.NewGuid(), address.ConversationId, runId, kind, participant, null, text, null,
            approval, toolRequest, null, null, runStatus, DateTimeOffset.UtcNow);
        return grain.RecordProgressAsync(new ConversationProgressInput(
            JsonSerializer.Serialize(progress, ConversationContractsJsonContext.Default.ConversationProgress)));
    }
}
