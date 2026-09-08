using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using ForgeMission.ConversationHost.Messaging;
using ForgeMission.ConversationHost.Persistence;
using ForgeMission.Conversations.Contracts;
using Orleans;
using Orleans.Runtime;

namespace ForgeMission.ConversationHost.Grains;

/// <summary>
/// The sole sequence allocator and event appender for one conversation. Fixed protocol for every
/// accept/record operation: (1) repair any prior pending transition (and, for the two commands that
/// begin a new run, any prior pending run start), (2) plan one event at <c>LastSequence + 1</c> and
/// persist it in <c>PendingTransition</c> first, (3) idempotently append it, (4) advance
/// <c>LastSequence</c>/update snapshot fields, publish it to <see cref="IConversationEventNotifier"/>,
/// and notify <c>MissionRunGrain</c> — except <see cref="RecordRunInterruptionAsync"/>, which
/// deliberately skips that notification (it is called only after MissionRunGrain has already
/// persisted its own terminal state, so calling back would be a synchronous re-entrant call into its
/// still-executing activation) — then (5) if the transition owes a mission-command send, dispatch it
/// (a resend after a broker accept is safe: the queue dedupes on <c>MessageId</c>) and only then
/// clear <c>PendingTransition</c>. A transition that owes a dispatch also owns a durable Orleans
/// reminder (<see cref="OutboxReminderName"/>) so a crash between steps is retried even if this
/// activation never restarts on its own.
///
/// <see cref="AcceptCommandAsync"/>, <see cref="AcceptFollowupCommandAsync"/>, and
/// <see cref="AcceptToolResultAsync"/> (Task 6) classify every expected client conflict — an active
/// run already existing, a mismatched/unknown/already-completed tool request, or a reused
/// command/event ID with different content — as a typed <see cref="ConversationCommandOutcomeResult"/>
/// they compare explicitly themselves. None of them call or catch exceptions from
/// <c>IConversationEventStore.AppendAsync</c> for that classification; <c>AppendAsync</c>'s own
/// equality guard remains only a storage-level integrity backstop for repair paths.
/// </summary>
public sealed class ConversationGrain(
    [PersistentState("conversation-checkpoint", "conversation-checkpoint")] IPersistentState<ConversationCheckpoint> checkpoint,
    IConversationEventStore eventStore,
    IProjectRunIndexStore projectRunIndexStore,
    IGrainFactory grainFactory,
    IConversationCommandDispatcher dispatcher,
    IConversationEventNotifier notifier)
    : Grain, IConversationGrain, IRemindable
{
    private const string OutboxReminderName = "mission-command-outbox";

    private static readonly TimeSpan OutboxReminderDueTime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan OutboxReminderPeriod = TimeSpan.FromMinutes(1);

    private ConversationAddress Address => ConversationAddress.Parse(this.GetPrimaryKeyString());
    private ProjectRunIndex ProjectRuns => new(eventStore, projectRunIndexStore);

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await RepairPendingTransitionIfAnyAsync(cancellationToken);
        await RepairPendingRunStartIfAnyAsync(cancellationToken);

        // A Table sequence beyond the checkpoint without the matching planned event is
        // corruption — it must not guess a missing transition.
        var address = Address;
        await foreach (var _ in eventStore.ReadAfterAsync(address, checkpoint.State.LastSequence, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Conversation '{address}' has a Table event beyond checkpoint LastSequence " +
                $"{checkpoint.State.LastSequence} with no pending transition to explain it — refusing to guess.");
        }

        await base.OnActivateAsync(cancellationToken);
    }

    /// <summary>The durable retry driver for the mission-command outbox beyond activation-triggered
    /// repair alone — fires periodically while a dispatch is owed. A tick with no pending
    /// transition means the reminder outlived its transition (an earlier unregister was lost); it
    /// is unregistered here as a safety net rather than left to fire forever.</summary>
    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        if (reminderName != OutboxReminderName)
            return;

        if (checkpoint.State.PendingTransition is null)
        {
            var reminder = await this.GetReminder(OutboxReminderName);
            if (reminder is not null)
                await this.UnregisterReminder(reminder);
            return;
        }

        await RepairPendingTransitionIfAnyAsync(CancellationToken.None);
    }

    public async Task<ConversationCommandOutcomeResult> AcceptCommandAsync(ConversationCommandInput input)
    {
        var ct = CancellationToken.None;
        await RepairPendingTransitionIfAnyAsync(ct);
        await RepairPendingRunStartIfAnyAsync(ct);

        var command = JsonSerializer.Deserialize(input.CommandJson, ConversationContractsJsonContext.Default.ConversationCommand)
            ?? throw new InvalidOperationException("CommandJson deserialized to null.");

        if (command.ConversationId != Address.ConversationId)
            throw new InvalidOperationException(
                $"Command conversation id '{command.ConversationId}' does not match this grain's address.");

        if (checkpoint.State.Purpose == ConversationPurpose.ProjectControl)
            return new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Conflict, null,
                "This legacy Project Control conversation is read-only.");

        // A MissionRun command names a run and carries no project goal. Both are guaranteed by the
        // HTTP adapter (StartConversationRequest has neither field), so these guard any OTHER
        // caller of this grain rather than a live route.
        if (command.RunId is null)
            return new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Invalid, null, "A mission-run command requires a run id.");

        if (command.ProjectGoal is not null)
            return new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Invalid, null,
                "A mission-run command cannot carry a project goal.");

        // Duplicate command acceptance resolves through the durable event-ID row (the UserMessage
        // event's EventId is the command's CommandId) — checked before allocating any new
        // sequence, so a retry never fights a fresh (wrong) sequence number against the original.
        var existing = await eventStore.FindByEventIdAsync(Address, command.CommandId, ct);
        if (existing is not null)
            return ResolveDuplicateStart(existing, command);

        // Unreachable via the Task 6 HTTP adapter: POST /conversations derives a fresh, previously
        // unused deterministic address per CommandId, so no live caller can present a NEW CommandId
        // against an ALREADY-pinned conversation with a different mission. Retained as a genuine
        // programming-error guard for any other caller of this grain.
        if (checkpoint.State.MissionRef is { Length: > 0 } pinned && pinned != command.MissionRef)
            throw new InvalidOperationException(
                $"Conversation is pinned to mission '{pinned}'; cannot accept '{command.MissionRef}'.");

        if (checkpoint.State.ActiveRunId is not null)
            return new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Conflict, null,
                $"Conversation already has an active run '{checkpoint.State.ActiveRunId}'.");

        // Pinned once, on the conversation's first-ever accepted start; folded into the SAME
        // checkpoint write BeginRunAsync performs below for ActiveStartCommandJson/PendingRunStart.
        if (string.IsNullOrEmpty(checkpoint.State.MissionRef))
        {
            checkpoint.State.TenantId = Address.TenantId;
            checkpoint.State.ConversationId = Address.ConversationId;
            checkpoint.State.MissionRef = command.MissionRef;
            checkpoint.State.PinnedCapabilitiesJson = JsonSerializer.Serialize(
                command.Capabilities, ConversationContractsJsonContext.Default.ConversationCapabilityDeclarationArray);
        }

        return await BeginRunAsync(command, ct);
    }

    public async Task<ConversationCommandOutcomeResult> AcceptFollowupCommandAsync(ConversationFollowupCommandInput input)
    {
        var ct = CancellationToken.None;
        await RepairPendingTransitionIfAnyAsync(ct);
        await RepairPendingRunStartIfAnyAsync(ct);

        if (checkpoint.State.Purpose == ConversationPurpose.ProjectControl)
            return new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Conflict, null,
                "This legacy Project Control conversation is read-only.");

        var existing = await eventStore.FindByEventIdAsync(Address, input.CommandId, ct);
        if (existing is not null)
            return ResolveDuplicateFollowup(existing, input);

        if (string.IsNullOrEmpty(checkpoint.State.MissionRef))
            return new ConversationCommandOutcomeResult(ConversationCommandOutcome.Conflict, null, "Conversation has no pinned mission.");

        if (checkpoint.State.ActiveRunId is not null)
            return new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Conflict, null,
                $"Conversation already has an active run '{checkpoint.State.ActiveRunId}'.");

        // The grain — never the caller — reconstructs mission/capabilities from what is already
        // pinned, so a follow-up can never select a different mission or replace capabilities.
        var capabilities = JsonSerializer.Deserialize(
            checkpoint.State.PinnedCapabilitiesJson ?? "[]",
            ConversationContractsJsonContext.Default.ConversationCapabilityDeclarationArray) ?? [];

        var command = new ConversationCommand(
            input.CommandId, Address.ConversationId, Guid.NewGuid(), ConversationCommandKind.StartMission,
            checkpoint.State.MissionRef, input.Text, capabilities, null, ProjectGoal: null);

        return await BeginRunAsync(command, ct);
    }

    // -- Project Mission acceptance (43.21 task 1) --

    public async Task<ConversationCommandOutcomeResult> AcceptProjectMissionContainerCreateAsync(
        ConversationProjectMissionCreateInput input)
    {
        var ct = CancellationToken.None;
        await RepairPendingTransitionIfAnyAsync(ct);

        if (input.CommandId == Guid.Empty || input.ProjectId == Guid.Empty || string.IsNullOrWhiteSpace(input.ProjectGoal))
            return new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Invalid, null,
                "A Project Mission container requires a command id, a project id, and a non-blank goal.");

        if (input.CommandId != ConversationDeterministicIds.ProjectMissionContainerCreate(input.ProjectId))
            return new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Invalid, null,
                "The Project Mission container command id does not match its Project id.");

        // Existence is read from purpose + Project ID, NOT from MissionRef: a container pins no
        // mission, so MissionRef stays null and cannot serve as the "already created" signal the
        // control path uses. An exact retry is recognised by the create's own content.
        if (checkpoint.State.Purpose == ConversationPurpose.ProjectMission && checkpoint.State.ProjectId is not null)
        {
            var isSameContainer =
                checkpoint.State.ProjectId == input.ProjectId &&
                string.Equals(checkpoint.State.ProjectGoal, input.ProjectGoal, StringComparison.Ordinal);

            return isSameContainer
                ? ContainerAccepted()
                : new ConversationCommandOutcomeResult(
                    ConversationCommandOutcome.Conflict, null,
                    "This container is already pinned to a different Project or goal.");
        }

        // Any OTHER already-initialised conversation — a Janus run, a control conversation — is a
        // conflict rather than being converted. Nothing repoints an existing conversation.
        if (!string.IsNullOrEmpty(checkpoint.State.MissionRef) || checkpoint.State.ProjectId is not null)
            return new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Conflict, null,
                "This conversation is already pinned to a different purpose.");

        checkpoint.State.TenantId = Address.TenantId;
        checkpoint.State.ConversationId = Address.ConversationId;
        checkpoint.State.Purpose = ConversationPurpose.ProjectMission;
        checkpoint.State.ProjectId = input.ProjectId;
        checkpoint.State.ProjectGoal = input.ProjectGoal;
        checkpoint.State.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await checkpoint.WriteStateAsync();

        // No event, nothing to dispatch, no run to begin — a newly created container is empty.
        return ContainerAccepted();

        ConversationCommandOutcomeResult ContainerAccepted() =>
            new(ConversationCommandOutcome.Accepted,
                new ConversationCommandAcceptance(
                    Address.ConversationId, null, checkpoint.State.LastSequence, checkpoint.State.Status),
                null);
    }

    public async Task<ConversationCommandOutcomeResult> AcceptProjectMissionRunAsync(ConversationProjectMissionRunInput input)
    {
        var ct = CancellationToken.None;
        await RepairPendingTransitionIfAnyAsync(ct);
        await RepairPendingRunStartIfAnyAsync(ct);

        // Message-shape validation comes first, before state and duplicate checks:
        // these are properties of the message, and a malformed one must never reach the
        // idempotency lookup under Guid.Empty where unrelated bad commands would collide.
        if (input.CommandId == Guid.Empty || string.IsNullOrWhiteSpace(input.Mission) || string.IsNullOrWhiteSpace(input.Input))
            return new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Invalid, null,
                "A Project Mission run requires a command id, a mission, and non-blank input.");

        var launch = string.IsNullOrWhiteSpace(input.LaunchJson) ? null : JsonSerializer.Deserialize(
            input.LaunchJson, ConversationContractsJsonContext.Default.DurableMissionLaunch);
        string? packageReason = null;
        if (launch is null && !ProjectMissionNames.IsKnown(input.Mission))
            return new ConversationCommandOutcomeResult(ConversationCommandOutcome.Invalid, null,
                "The Project Mission is not supported.");
        if (launch is not null && (!string.Equals(input.Mission, "Durable", StringComparison.Ordinal) ||
            !DurableMissionPackageAdmission.TryValidate(launch, out packageReason)))
            return new ConversationCommandOutcomeResult(ConversationCommandOutcome.Invalid, null,
                packageReason ?? "The immutable durable package is invalid.");

        if (checkpoint.State.Purpose != ConversationPurpose.ProjectMission)
            return new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Conflict, null, "This conversation is not a Project Mission container.");

        // The command is built BEFORE the duplicate lookup so a retry can be compared against what
        // this call would have produced — mission and input included, which is what makes "same
        // command id, changed mission" a conflict rather than a silently accepted second run.
        // ProjectGoal comes from the container's pinned checkpoint state and from nowhere else —
        // StartProjectMissionRunRequest has no such member, so no caller can supply or replace it.
        // It is set for BOTH missions rather than only the one that reads it: every child command
        // of a Project is then identically shaped, which is the property this task exists to
        // establish. Janus simply ignores a value it has no parameter for.
        var command = new ConversationCommand(
            input.CommandId, Address.ConversationId,
            ConversationDeterministicIds.ProjectMissionRun(input.CommandId),
            ConversationCommandKind.StartMission,
            // ZERO capabilities, always. Opening or invoking a Project grants no local tool
            // authority (43.21), so this is an empty literal rather than a value read from
            // anywhere: there is no member on the input, and no checkpoint state, that could make
            // it non-empty. A direct Host caller therefore has nothing to smuggle a tool
            // declaration through.
            input.Mission, input.Input, [], null, checkpoint.State.ProjectGoal, launch);

        var existing = await eventStore.FindByEventIdAsync(Address, input.CommandId, ct);
        if (existing is not null)
            return ResolveDuplicateStart(existing, command);

        // One active run per Project (43.21 MVP). Reported as its own outcome rather than a
        // generic conflict: it is an ordinary product state a surface explains, not a malformed
        // request, and it appends nothing.
        if (checkpoint.State.ActiveRunId is not null)
            return new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.RunAlreadyActive, null,
                $"This Project already has an active mission run '{checkpoint.State.ActiveRunId}'.");

        // Deliberately no MissionRef or PinnedCapabilitiesJson write: the container pins neither,
        // and a run's mission lives in its own command. That is the whole reason a Project can
        // alternate between Janus and Naive without a second container.
        return await BeginRunAsync(command, ct);
    }

    // -- Phase 45.2 Mission Conversation / hidden Evaluation acceptance --

    public async Task<ConversationCommandOutcomeResult> AcceptMissionConversationCreateAsync(MissionConversationCreateInput input)
    {
        var ct = CancellationToken.None;
        await RepairPendingTransitionIfAnyAsync(ct);
        var launch = JsonSerializer.Deserialize(input.LaunchJson, ConversationContractsJsonContext.Default.DurableMissionLaunch);
        string? reason = null;
        if (input.CommandId == Guid.Empty || input.ProjectId == Guid.Empty || !DurableMissionPackageAdmission.TryValidate(launch, out reason))
            return new ConversationCommandOutcomeResult(ConversationCommandOutcome.Invalid, null, reason ?? "A Mission Conversation requires a valid immutable launch.");
        if (checkpoint.State.Purpose == ConversationPurpose.MissionConversation)
        {
            var pinned = DeserializeMissionHands(checkpoint.State.MissionConversationLaunchJson,
                ConversationContractsJsonContext.Default.DurableMissionLaunch);
            return checkpoint.State.ProjectId == input.ProjectId && pinned is not null && DurableMissionLaunchComparison.SameLaunch(pinned, launch!)
                ? new ConversationCommandOutcomeResult(ConversationCommandOutcome.Accepted,
                    new ConversationCommandAcceptance(Address.ConversationId, null, checkpoint.State.LastSequence, checkpoint.State.Status), null)
                : new ConversationCommandOutcomeResult(ConversationCommandOutcome.Conflict, null, "This Mission Conversation is already pinned differently.");
        }
        if (Exists)
            return new ConversationCommandOutcomeResult(ConversationCommandOutcome.Conflict, null, "This conversation is already pinned to another purpose.");
        checkpoint.State.TenantId = Address.TenantId;
        checkpoint.State.ConversationId = Address.ConversationId;
        checkpoint.State.Purpose = ConversationPurpose.MissionConversation;
        checkpoint.State.ProjectId = input.ProjectId;
        checkpoint.State.MissionConversationLaunchJson = JsonSerializer.Serialize(launch, ConversationContractsJsonContext.Default.DurableMissionLaunch);
        checkpoint.State.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await checkpoint.WriteStateAsync();
        return new ConversationCommandOutcomeResult(ConversationCommandOutcome.Accepted,
            new ConversationCommandAcceptance(Address.ConversationId, null, 0, checkpoint.State.Status), null);
    }

    public async Task<ConversationCommandOutcomeResult> AcceptMissionConversationTurnAsync(MissionConversationTurnInput input)
    {
        var ct = CancellationToken.None;
        await RepairPendingTransitionIfAnyAsync(ct);
        await RepairPendingRunStartIfAnyAsync(ct);
        if (checkpoint.State.Purpose != ConversationPurpose.MissionConversation || checkpoint.State.ProjectId is null)
            return new ConversationCommandOutcomeResult(ConversationCommandOutcome.Conflict, null, "This conversation is not a Mission Conversation.");
        if (input.CommandId == Guid.Empty || (!input.Retry && string.IsNullOrWhiteSpace(input.Text)))
            return new ConversationCommandOutcomeResult(ConversationCommandOutcome.Invalid, null, "A turn requires a command id and text.");
        var launch = DeserializeMissionHands(checkpoint.State.MissionConversationLaunchJson,
            ConversationContractsJsonContext.Default.DurableMissionLaunch);
        string? reason = null;
        if (!DurableMissionPackageAdmission.TryValidate(launch, out reason))
            return new ConversationCommandOutcomeResult(ConversationCommandOutcome.Conflict, null, reason ?? "The pinned launch is invalid.");
        var turnId = input.Retry ? input.TurnId : ConversationDeterministicIds.MissionTurn(input.CommandId);
        if (turnId is null || turnId == Guid.Empty)
            return new ConversationCommandOutcomeResult(ConversationCommandOutcome.Invalid, null, "A retry requires its original turn id.");
        var original = input.Retry ? await FindMissionTurnAsync(turnId.Value, ct) : null;
        if (input.Retry && original is null)
            return new ConversationCommandOutcomeResult(ConversationCommandOutcome.NotFound, null, "The turn was not found.");
        var existing = await eventStore.FindByEventIdAsync(Address, input.CommandId, ct);
        if (existing is not null)
        {
            var accepted = existing.AcceptedCommandJson is null ? null : JsonSerializer.Deserialize(existing.AcceptedCommandJson, ConversationContractsJsonContext.Default.ConversationCommand);
            return accepted is not null && accepted.TurnId == turnId
                ? new ConversationCommandOutcomeResult(ConversationCommandOutcome.Accepted,
                    new ConversationCommandAcceptance(Address.ConversationId, accepted.RunId, existing.Event.Sequence + 1, ConversationRunStatus.Queued, accepted.TurnId), null)
                : new ConversationCommandOutcomeResult(ConversationCommandOutcome.Conflict, null, "The command id was already used differently.");
        }
        if (checkpoint.State.ActiveRunId is not null)
            return new ConversationCommandOutcomeResult(ConversationCommandOutcome.RunAlreadyActive, null, "This Mission Conversation already has an active turn.");
        var text = input.Text;
        if (input.Retry)
        {
            text = original!.Goal;
        }
        var command = new ConversationCommand(input.CommandId, Address.ConversationId,
            ConversationDeterministicIds.MissionTurnAttempt(input.CommandId), ConversationCommandKind.StartMission,
            "Durable", text!, [], null, Launch: launch, TurnId: turnId);
        return await BeginRunAsync(command, ct);
    }

    public async Task<ConversationCommandOutcomeResult> AcceptEvaluationCreateAsync(EvaluationCreateInput input)
    {
        var request = JsonSerializer.Deserialize(input.RequestJson, ConversationContractsJsonContext.Default.StartEvaluationRequest);
        if (request is null || request.EvaluationResultId == Guid.Empty || request.ProjectId == Guid.Empty || request.MissionId == Guid.Empty ||
            request.MissionVersionId == Guid.Empty || request.EvaluationCaseId == Guid.Empty || request.CandidateRevision <= 0 || string.IsNullOrWhiteSpace(request.Input) ||
            request.Launch.MissionVersionId != request.MissionVersionId || !string.Equals(request.Launch.DefinitionHash, request.DefinitionHash, StringComparison.Ordinal))
            return new ConversationCommandOutcomeResult(ConversationCommandOutcome.Invalid, null, "The evaluation request is invalid.");
        if (!DurableMissionPackageAdmission.TryValidate(request.Launch, out var reason))
            return new ConversationCommandOutcomeResult(ConversationCommandOutcome.Invalid, null, reason ?? "The evaluation launch is invalid.");
        if (checkpoint.State.Purpose == ConversationPurpose.Evaluation)
            return checkpoint.State.EvaluationResultId == request.EvaluationResultId
                ? new ConversationCommandOutcomeResult(ConversationCommandOutcome.Accepted,
                    new ConversationCommandAcceptance(Address.ConversationId, checkpoint.State.ActiveRunId, checkpoint.State.LastSequence, checkpoint.State.Status), null)
                : new ConversationCommandOutcomeResult(ConversationCommandOutcome.Conflict, null, "This evaluation is already pinned differently.");
        if (Exists) return new ConversationCommandOutcomeResult(ConversationCommandOutcome.Conflict, null, "This conversation is already pinned to another purpose.");
        checkpoint.State.TenantId = Address.TenantId;
        checkpoint.State.ConversationId = Address.ConversationId;
        checkpoint.State.Purpose = ConversationPurpose.Evaluation;
        checkpoint.State.ProjectId = request.ProjectId;
        checkpoint.State.EvaluationResultId = request.EvaluationResultId;
        checkpoint.State.EvaluationCaseId = request.EvaluationCaseId;
        checkpoint.State.EvaluationTurnId = ConversationDeterministicIds.EvaluationTurn(request.EvaluationResultId);
        checkpoint.State.EvaluationTurnAttemptId = ConversationDeterministicIds.MissionTurnAttempt(request.EvaluationResultId);
        checkpoint.State.EvaluationDeclaredProfile = request.Launch.Profile;
        // Candidate profile is provenance only. Text-only evaluations always execute through the
        // already accepted generic Worker path with NoHands and no Bob attachment.
        var executionLaunch = request.Launch with { Profile = MissionHandsProfile.NoHands };
        checkpoint.State.MissionConversationLaunchJson = JsonSerializer.Serialize(executionLaunch, ConversationContractsJsonContext.Default.DurableMissionLaunch);
        await checkpoint.WriteStateAsync();
        var command = new ConversationCommand(request.EvaluationResultId, Address.ConversationId, checkpoint.State.EvaluationTurnAttemptId,
            ConversationCommandKind.StartMission, "Durable", request.Input, [], null, Launch: executionLaunch, TurnId: checkpoint.State.EvaluationTurnId);
        return await BeginRunAsync(command, CancellationToken.None);
    }

    public async Task<ConversationCommandOutcomeResult> CancelMissionConversationTurnAsync(MissionConversationCancelInput input)
    {
        var ct = CancellationToken.None;
        await RepairPendingTransitionIfAnyAsync(ct);
        if (checkpoint.State.Purpose != ConversationPurpose.MissionConversation || input.CommandId == Guid.Empty || input.TurnId == Guid.Empty || input.TurnAttemptId == Guid.Empty)
            return new ConversationCommandOutcomeResult(ConversationCommandOutcome.Invalid, null, "The cancellation request is invalid.");
        var existing = await eventStore.FindByEventIdAsync(Address, input.CommandId, ct);
        if (existing is not null)
            return new ConversationCommandOutcomeResult(ConversationCommandOutcome.Accepted,
                new ConversationCommandAcceptance(Address.ConversationId, input.TurnAttemptId, existing.Event.Sequence, existing.Event.RunStatus ?? checkpoint.State.Status), null);
        if (checkpoint.State.ActiveRunId != input.TurnAttemptId)
        {
            var terminal = await ReadTerminalMissionTurnAsync(input.TurnId, input.TurnAttemptId, ct);
            return terminal is null
                ? new ConversationCommandOutcomeResult(ConversationCommandOutcome.Conflict, null, "The cancellation does not match an active or terminal turn.")
                : new ConversationCommandOutcomeResult(ConversationCommandOutcome.Accepted,
                    new ConversationCommandAcceptance(Address.ConversationId, input.TurnAttemptId, terminal.Sequence, terminal.RunStatus!.Value, input.TurnId), null);
        }
        var start = checkpoint.State.ActiveStartCommandJson is null ? null : JsonSerializer.Deserialize(checkpoint.State.ActiveStartCommandJson, ConversationContractsJsonContext.Default.ConversationCommand);
        if (start?.TurnId != input.TurnId)
            return new ConversationCommandOutcomeResult(ConversationCommandOutcome.Conflict, null, "The cancellation does not match the active turn.");
        var cancelled = new ConversationEvent(input.CommandId, 1, Address.ConversationId, input.TurnAttemptId, checkpoint.State.LastSequence + 1,
            ConversationEventKind.RunStatus, ConversationParticipant.Forge, null, null, "Cancelled by operator.", null, null, null, null,
            ConversationRunStatus.Interrupted, DateTimeOffset.UtcNow);
        var stored = await PlanAppendAdvanceAsync(cancelled, null, notifyMissionRun: true, dispatchCommand: null, ct);
        return new ConversationCommandOutcomeResult(ConversationCommandOutcome.Accepted,
            new ConversationCommandAcceptance(Address.ConversationId, input.TurnAttemptId, stored.Sequence, ConversationRunStatus.Interrupted, input.TurnId), null);
    }

    public async Task<ConversationCommandOutcomeResult> AcceptToolResultAsync(ConversationToolResultInput input)
    {
        var ct = CancellationToken.None;
        await RepairPendingTransitionIfAnyAsync(ct);

        // Resolved BEFORE checking current run state — an exact replay of an already-accepted tool
        // result must return its original acceptance even after the run has since gone terminal and
        // ExpectedToolRequestId/ActiveRunId no longer reflect it.
        if (checkpoint.State.Purpose == ConversationPurpose.ProjectControl)
            return new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Conflict, null, "This legacy Project Control conversation is read-only.");

        var existing = await eventStore.FindByEventIdAsync(Address, input.CommandId, ct);
        if (existing is not null)
            return ResolveDuplicateToolResult(existing, input);

        if (checkpoint.State.ActiveRunId is not { } activeRunId)
            return new ConversationCommandOutcomeResult(ConversationCommandOutcome.Conflict, null, "No active run.");

        if (checkpoint.State.ExpectedToolRequestId is not { } expected || expected != input.ToolRequestId)
            return new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Conflict, null, "Tool result does not match the expected outstanding tool request.");

        // The matching participant is Implementer: this completes the Implementer's declared tool
        // hand-off. Forge stays reserved for infrastructure/lifecycle facts (RunStatus, Error).
        var progress = new ConversationProgress(
            input.CommandId, Address.ConversationId, activeRunId, ConversationEventKind.ToolResult, ConversationParticipant.Implementer,
            null, null, null, null, null,
            new ConversationToolResult(input.ToolRequestId, input.Content, input.IsError), null, null, DateTimeOffset.UtcNow);
        var progressJson = JsonSerializer.Serialize(progress, ConversationContractsJsonContext.Default.ConversationProgress);
        var progressByteCount = Encoding.UTF8.GetByteCount(progressJson);
        if (progressByteCount > ConversationJsonLimits.MaxInlineEventJsonBytes)
            return new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Invalid, null,
                $"Tool result content ({progressByteCount} bytes) exceeds the " +
                $"{ConversationJsonLimits.MaxInlineEventJsonBytes}-byte limit.");

        // Reuses RecordProgressAsync's existing expected-request/deterministic-continuation
        // semantics rather than forking them; its own duplicate check is a harmless no-op re-read
        // here, since the lookup above already proved input.CommandId is not yet recorded.
        var progressAcceptance = await RecordProgressAsync(new ConversationProgressInput(progressJson));

        return progressAcceptance.Outcome switch
        {
            ConversationProgressOutcome.Appended or ConversationProgressOutcome.AlreadyRecorded =>
                new ConversationCommandOutcomeResult(
                    ConversationCommandOutcome.Accepted,
                    new ConversationCommandAcceptance(
                        Address.ConversationId, activeRunId, progressAcceptance.Sequence!.Value, ConversationRunStatus.WaitingForTool),
                    null),
            ConversationProgressOutcome.Rejected =>
                new ConversationCommandOutcomeResult(ConversationCommandOutcome.Conflict, null, progressAcceptance.RejectionReason),
            _ => throw new InvalidOperationException($"Unhandled {nameof(ConversationProgressOutcome)} '{progressAcceptance.Outcome}'."),
        };
    }

    // Generic mission hands is deliberately part of this grain: ConversationGrain remains the
    // sole canonical event/sequence owner. Task C supplies requests from the generic Worker; it
    // cannot create a second durable ledger or bypass these exact correlation checks.
    public async Task<MissionHandsGrainResult> AttachMissionHandsAsync(MissionHandsJsonInput input)
    {
        await RepairPendingTransitionIfAnyAsync(CancellationToken.None);
        var attachment = JsonSerializer.Deserialize(input.Json, ConversationContractsJsonContext.Default.AttachMissionHandsRequest);
        if (!Exists || attachment is null || attachment.ConversationId != Address.ConversationId || attachment.AttachmentId == Guid.Empty ||
            attachment.ApplicationSessionId == Guid.Empty || !ValidMissionHandsLaunch(attachment.Launch))
            return MissionHandsReject("Invalid mission hands attachment.");
        var pinned = DeserializeMissionHands(checkpoint.State.MissionHandsLaunchJson,
            ConversationContractsJsonContext.Default.DurableMissionLaunch);
        if (pinned is not null && !DurableMissionLaunchComparison.SameLaunch(pinned, attachment.Launch))
            return MissionHandsReject("The attachment launch does not match the pinned approved launch.");

        checkpoint.State.MissionHandsLaunchJson ??= JsonSerializer.Serialize(
            attachment.Launch, ConversationContractsJsonContext.Default.DurableMissionLaunch);
        var priorAttachment = DeserializeMissionHands(checkpoint.State.MissionHandsAttachmentJson,
            ConversationContractsJsonContext.Default.MissionHandsAttachment);
        var freshAttachment = new MissionHandsAttachment(
            attachment.ConversationId, attachment.AttachmentId, attachment.ApplicationSessionId, attachment.Launch, DateTimeOffset.UtcNow);
        if (priorAttachment is null || priorAttachment.AttachmentId == attachment.AttachmentId)
        {
            checkpoint.State.MissionHandsAttachmentJson = JsonSerializer.Serialize(freshAttachment,
                ConversationContractsJsonContext.Default.MissionHandsAttachment);
        }
        else if (checkpoint.State.MissionHandsInFlight)
        {
            // The old Bob may still hold a real side effect. Preserve its active attachment and
            // retain the claim; this fresh registration has no work until old Bob drains and the
            // old attachment durably detaches.
            var pendingAttachment = DeserializeMissionHands(checkpoint.State.MissionHandsPendingAttachmentJson,
                ConversationContractsJsonContext.Default.MissionHandsAttachment);
            if (pendingAttachment is not null && pendingAttachment.AttachmentId != freshAttachment.AttachmentId)
                return MissionHandsReject("A mission hands attachment handoff is already pending.");
            checkpoint.State.MissionHandsPendingAttachmentJson = JsonSerializer.Serialize(freshAttachment,
                ConversationContractsJsonContext.Default.MissionHandsAttachment);
        }
        else
        {
            checkpoint.State.MissionHandsAttachmentJson = JsonSerializer.Serialize(freshAttachment,
                ConversationContractsJsonContext.Default.MissionHandsAttachment);
        }
        checkpoint.State.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await checkpoint.WriteStateAsync();
        return MissionHandsAccept(checkpoint.State.MissionHandsInFlight ? MissionHandsStatus.InFlight : MissionHandsStatus.Attached,
            checkpoint.State.LastSequence);
    }

    public async Task<MissionHandsGrainResult> DetachMissionHandsAsync(MissionHandsJsonInput input)
    {
        await RepairPendingTransitionIfAnyAsync(CancellationToken.None);
        var request = JsonSerializer.Deserialize(input.Json, ConversationContractsJsonContext.Default.DetachMissionHandsRequest);
        var attachment = DeserializeMissionHands(checkpoint.State.MissionHandsAttachmentJson,
            ConversationContractsJsonContext.Default.MissionHandsAttachment);
        if (request is null || request.ConversationId != Address.ConversationId || attachment?.AttachmentId != request.AttachmentId)
            return MissionHandsReject("The mission hands attachment is stale or foreign.");
        var pendingAttachment = DeserializeMissionHands(checkpoint.State.MissionHandsPendingAttachmentJson,
            ConversationContractsJsonContext.Default.MissionHandsAttachment);
        checkpoint.State.MissionHandsAttachmentJson = pendingAttachment is null ? null : JsonSerializer.Serialize(
            pendingAttachment, ConversationContractsJsonContext.Default.MissionHandsAttachment);
        checkpoint.State.MissionHandsPendingAttachmentJson = null;
        // A detach is the durable acknowledgement that Application has already cancelled/drained
        // this attachment's Bob. Only here may its claim be requeued for a pending fresh attach.
        checkpoint.State.MissionHandsInFlight = false;
        checkpoint.State.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await checkpoint.WriteStateAsync();
        return MissionHandsAccept(checkpoint.State.MissionHandsRequestJson is null ? MissionHandsStatus.Completed :
            pendingAttachment is null ? MissionHandsStatus.AwaitingHands : MissionHandsStatus.Attached,
            checkpoint.State.LastSequence);
    }

    public async Task<MissionHandsGrainResult> RecordMissionHandsRequestAsync(MissionHandsJsonInput input)
    {
        var ct = CancellationToken.None;
        await RepairPendingTransitionIfAnyAsync(ct);
        var request = JsonSerializer.Deserialize(input.Json, ConversationContractsJsonContext.Default.MissionToolRequest);
        if (!Exists || request is null || request.ConversationId != Address.ConversationId || request.ToolRequestId == Guid.Empty ||
            request.TurnAttemptId == Guid.Empty || string.IsNullOrWhiteSpace(request.ToolName) ||
            string.IsNullOrWhiteSpace(request.OpaqueContinuation) || checkpoint.State.MissionHandsLaunchJson is null)
            return MissionHandsReject("Invalid mission hands request.");
        var outstanding = DeserializeMissionHands(checkpoint.State.MissionHandsRequestJson,
            ConversationContractsJsonContext.Default.MissionToolRequest);
        if (outstanding is not null)
            return outstanding.ToolRequestId == request.ToolRequestId
                ? MissionHandsAccept(checkpoint.State.MissionHandsAttachmentJson is null ? MissionHandsStatus.AwaitingHands : MissionHandsStatus.Attached,
                    checkpoint.State.LastSequence)
                : MissionHandsReject("A mission attempt already has an outstanding hands request.");
        if (checkpoint.State.MissionHandsCompletedToolRequestId == request.ToolRequestId)
            return MissionHandsReject("The mission hands request is late.");

        checkpoint.State.MissionHandsRequestJson = JsonSerializer.Serialize(request,
            ConversationContractsJsonContext.Default.MissionToolRequest);
        checkpoint.State.MissionHandsCancelled = false;
        checkpoint.State.MissionHandsInterrupted = false;
        checkpoint.State.MissionHandsTerminalReason = null;
        checkpoint.State.MissionHandsInFlight = false;
        var requested = new ConversationEvent(request.ToolRequestId, 1, Address.ConversationId, checkpoint.State.ActiveRunId,
            checkpoint.State.LastSequence + 1, ConversationEventKind.MissionHandsRequested, ConversationParticipant.Forge,
            null, null, null, null, null, null, null, null, DateTimeOffset.UtcNow, MissionHandsRequest: request);
        await PlanAppendAdvanceAsync(requested, null, notifyMissionRun: false, dispatchCommand: null, ct);
        if (checkpoint.State.MissionHandsAttachmentJson is not null)
            return MissionHandsAccept(MissionHandsStatus.Attached, checkpoint.State.LastSequence);

        var awaiting = new ConversationEvent(ConversationDeterministicIds.MissionHandsAwaiting(request.ToolRequestId), 1,
            Address.ConversationId, checkpoint.State.ActiveRunId, checkpoint.State.LastSequence + 1,
            ConversationEventKind.MissionHandsAwaiting, ConversationParticipant.Forge, null, null,
            "Awaiting a live Application/Bob attachment.", null, null, null, null, null, DateTimeOffset.UtcNow);
        await PlanAppendAdvanceAsync(awaiting, null, notifyMissionRun: false, dispatchCommand: null, ct);
        return MissionHandsAccept(MissionHandsStatus.AwaitingHands, checkpoint.State.LastSequence);
    }

    public async Task<MissionHandsGrainResult> AcceptMissionHandsResultAsync(MissionHandsJsonInput input)
    {
        var ct = CancellationToken.None;
        await RepairPendingTransitionIfAnyAsync(ct);
        var result = JsonSerializer.Deserialize(input.Json, ConversationContractsJsonContext.Default.SubmitMissionToolResultRequest);
        if (!Exists || result is null || result.ConversationId != Address.ConversationId || result.AttachmentId == Guid.Empty ||
            result.TurnAttemptId == Guid.Empty || result.CommandId == Guid.Empty || result.ToolRequestId == Guid.Empty ||
            !Enum.IsDefined(result.Outcome))
            return MissionHandsReject("Invalid mission hands result.");
        var canonicalResult = JsonSerializer.Serialize(result,
            ConversationContractsJsonContext.Default.SubmitMissionToolResultRequest);
        if (checkpoint.State.MissionHandsCompletedCommandId == result.CommandId &&
            checkpoint.State.MissionHandsCompletedToolRequestId == result.ToolRequestId)
            return checkpoint.State.MissionHandsCompletedResultJson == canonicalResult
                ? MissionHandsAccept(MissionHandsStatus.Completed, checkpoint.State.MissionHandsResultSequence)
                : MissionHandsReject("A replayed mission hands result does not exactly match the accepted result.");
        if (checkpoint.State.MissionHandsCompletedToolRequestId == result.ToolRequestId)
            return MissionHandsReject("The mission hands result is late or uses a different command.");
        var attachment = DeserializeMissionHands(checkpoint.State.MissionHandsAttachmentJson,
            ConversationContractsJsonContext.Default.MissionHandsAttachment);
        var outstanding = DeserializeMissionHands(checkpoint.State.MissionHandsRequestJson,
            ConversationContractsJsonContext.Default.MissionToolRequest);
        if (attachment?.AttachmentId != result.AttachmentId || outstanding is null ||
            outstanding.ToolRequestId != result.ToolRequestId || outstanding.TurnAttemptId != result.TurnAttemptId ||
            !checkpoint.State.MissionHandsInFlight)
            return MissionHandsReject("The mission hands result is wrong, late, or detached.");

        var startCommand = JsonSerializer.Deserialize(checkpoint.State.ActiveStartCommandJson
                ?? throw new InvalidOperationException("No active generic start command for a mission hands result."),
            ConversationContractsJsonContext.Default.ConversationCommand)
            ?? throw new InvalidOperationException("Active generic start command deserialized to null.");
        var continuation = startCommand with
        {
            CommandId = ConversationDeterministicIds.MissionHandsContinuation(result.ToolRequestId),
            Kind = ConversationCommandKind.ContinueAfterTool,
            ToolResult = new ConversationToolResult(result.ToolRequestId, result.Content ?? result.Reason ?? string.Empty,
                result.Outcome != MissionToolOutcome.Succeeded),
            OpaqueContinuation = outstanding.OpaqueContinuation,
            ProviderToolCallId = outstanding.ProviderToolCallId,
        };
        checkpoint.State.MissionHandsExpectedCommandId = continuation.CommandId;
        var fact = new ConversationEvent(result.CommandId, 1, Address.ConversationId, checkpoint.State.ActiveRunId,
            checkpoint.State.LastSequence + 1, ConversationEventKind.MissionHandsResult, ConversationParticipant.Forge,
            null, result.Content, result.Reason, null, null, null, null, null, DateTimeOffset.UtcNow,
            MissionHandsOutcome: result.Outcome);
        await PlanAppendAdvanceAsync(fact, null, notifyMissionRun: false, dispatchCommand: continuation, ct);
        checkpoint.State.MissionHandsRequestJson = null;
        checkpoint.State.MissionHandsAwaitingToolConfirmation = false;
        checkpoint.State.MissionHandsInFlight = false;
        checkpoint.State.MissionHandsCompletedToolRequestId = result.ToolRequestId;
        checkpoint.State.MissionHandsCompletedCommandId = result.CommandId;
        checkpoint.State.MissionHandsResultSequence = checkpoint.State.LastSequence;
        checkpoint.State.MissionHandsCompletedResultJson = canonicalResult;
        await checkpoint.WriteStateAsync();
        // Task C uses MissionHandsContinuation(toolRequestId) once, after this canonical fact;
        // no second result can reach that later dispatch because this state is terminalized here.
        return MissionHandsAccept(MissionHandsStatus.Completed, checkpoint.State.LastSequence);
    }

    /// <summary>Returns the one Host-owned outstanding request only to its exact live
    /// Application attachment.  This is deliberately a grain query rather than an adapter
    /// reconstruction: a caller cannot manufacture a tool name, arguments, continuation, or
    /// correlation that Bob might execute.</summary>
    public async Task<MissionHandsGrainResult> GetMissionHandsWorkAsync(MissionHandsJsonInput input)
    {
        await RepairPendingTransitionIfAnyAsync(CancellationToken.None);
        var query = JsonSerializer.Deserialize(input.Json, ConversationContractsJsonContext.Default.GetMissionHandsWorkRequest);
        var attachment = DeserializeMissionHands(checkpoint.State.MissionHandsAttachmentJson,
            ConversationContractsJsonContext.Default.MissionHandsAttachment);
        var pendingAttachment = DeserializeMissionHands(checkpoint.State.MissionHandsPendingAttachmentJson,
            ConversationContractsJsonContext.Default.MissionHandsAttachment);
        var outstanding = DeserializeMissionHands(checkpoint.State.MissionHandsRequestJson,
            ConversationContractsJsonContext.Default.MissionToolRequest);
        if (!Exists || query is null || query.ConversationId != Address.ConversationId)
            return MissionHandsWorkReject("The mission hands attachment is stale or foreign.");
        if (checkpoint.State.MissionHandsInFlight && pendingAttachment?.AttachmentId == query.AttachmentId &&
            pendingAttachment.ApplicationSessionId == query.ApplicationSessionId)
            return MissionHandsWork(MissionHandsStatus.InFlight, pendingAttachment, null, checkpoint.State.LastSequence);
        if (attachment?.AttachmentId != query.AttachmentId || attachment.ApplicationSessionId != query.ApplicationSessionId)
            return MissionHandsWorkReject("The mission hands attachment is stale or foreign.");

        var status = checkpoint.State.MissionHandsCancelled ? MissionHandsStatus.Cancelled :
            checkpoint.State.MissionHandsInterrupted ? MissionHandsStatus.Interrupted :
            outstanding is null ? MissionHandsStatus.Completed :
            checkpoint.State.MissionHandsInFlight ? MissionHandsStatus.InFlight :
            checkpoint.State.MissionHandsAwaitingToolConfirmation ? MissionHandsStatus.AwaitingToolConfirmation : MissionHandsStatus.Attached;
        return MissionHandsWork(status, attachment, outstanding, checkpoint.State.LastSequence, checkpoint.State.MissionHandsTerminalReason);
    }

    /// <summary>Atomically transitions the exact Host-owned outstanding request to in-flight.
    /// Only the caller receiving its payload may admit Bob; concurrent callers receive no
    /// payload. A replacement attachment remains payload-free until Application has drained Bob
    /// and the active attachment has durably detached.</summary>
    public async Task<MissionHandsGrainResult> ClaimMissionHandsWorkAsync(MissionHandsJsonInput input)
    {
        var ct = CancellationToken.None;
        await RepairPendingTransitionIfAnyAsync(ct);
        var query = JsonSerializer.Deserialize(input.Json, ConversationContractsJsonContext.Default.ClaimMissionHandsWorkRequest);
        var attachment = DeserializeMissionHands(checkpoint.State.MissionHandsAttachmentJson,
            ConversationContractsJsonContext.Default.MissionHandsAttachment);
        var pendingAttachment = DeserializeMissionHands(checkpoint.State.MissionHandsPendingAttachmentJson,
            ConversationContractsJsonContext.Default.MissionHandsAttachment);
        var outstanding = DeserializeMissionHands(checkpoint.State.MissionHandsRequestJson,
            ConversationContractsJsonContext.Default.MissionToolRequest);
        if (!Exists || query is null || query.ConversationId != Address.ConversationId)
            return MissionHandsWorkReject("There is no current mission hands request for this attachment.");
        if (checkpoint.State.MissionHandsInFlight && pendingAttachment?.AttachmentId == query.AttachmentId &&
            pendingAttachment.ApplicationSessionId == query.ApplicationSessionId)
            return MissionHandsWork(MissionHandsStatus.InFlight, pendingAttachment, null, checkpoint.State.LastSequence);
        if (attachment?.AttachmentId != query.AttachmentId || attachment.ApplicationSessionId != query.ApplicationSessionId ||
            outstanding is null || checkpoint.State.MissionHandsCancelled)
            return MissionHandsWorkReject("There is no current mission hands request for this attachment.");
        if (checkpoint.State.MissionHandsInFlight)
            return MissionHandsWork(MissionHandsStatus.InFlight, attachment, null, checkpoint.State.LastSequence);

        checkpoint.State.MissionHandsInFlight = true;
        var fact = new ConversationEvent(ConversationDeterministicIds.MissionHandsClaim(outstanding.ToolRequestId, attachment.AttachmentId), 1,
            Address.ConversationId, checkpoint.State.ActiveRunId, checkpoint.State.LastSequence + 1,
            ConversationEventKind.MissionHandsInFlight, ConversationParticipant.Forge, null,
            "Mission hands request claimed for Bob.", null, null, null, null, null, null, DateTimeOffset.UtcNow);
        await PlanAppendAdvanceAsync(fact, null, notifyMissionRun: false, dispatchCommand: null, ct);
        return MissionHandsWork(MissionHandsStatus.InFlight, attachment, outstanding, checkpoint.State.LastSequence);
    }

    public async Task<MissionHandsGrainResult> BeginMissionHandsConfirmationAsync(MissionHandsJsonInput input)
    {
        var ct = CancellationToken.None;
        await RepairPendingTransitionIfAnyAsync(ct);
        var request = JsonSerializer.Deserialize(input.Json, ConversationContractsJsonContext.Default.BeginMissionHandsConfirmationRequest);
        var attachment = DeserializeMissionHands(checkpoint.State.MissionHandsAttachmentJson,
            ConversationContractsJsonContext.Default.MissionHandsAttachment);
        var outstanding = DeserializeMissionHands(checkpoint.State.MissionHandsRequestJson,
            ConversationContractsJsonContext.Default.MissionToolRequest);
        if (!Exists || request is null || request.ConversationId != Address.ConversationId ||
            attachment?.AttachmentId != request.AttachmentId || outstanding?.ToolRequestId != request.ToolRequestId || checkpoint.State.MissionHandsCancelled)
            return MissionHandsReject("The mission hands confirmation is stale, foreign, or already terminal.");
        if (checkpoint.State.MissionHandsAwaitingToolConfirmation)
            return MissionHandsAccept(MissionHandsStatus.AwaitingToolConfirmation, checkpoint.State.LastSequence);

        checkpoint.State.MissionHandsAwaitingToolConfirmation = true;
        var fact = new ConversationEvent(ConversationDeterministicIds.MissionHandsConfirmation(request.ToolRequestId), 1,
            Address.ConversationId, checkpoint.State.ActiveRunId, checkpoint.State.LastSequence + 1,
            ConversationEventKind.MissionHandsAwaitingToolConfirmation, ConversationParticipant.Forge, null,
            "Awaiting local Bob confirmation.", null, null, null, null, null, null, DateTimeOffset.UtcNow);
        await PlanAppendAdvanceAsync(fact, null, notifyMissionRun: false, dispatchCommand: null, ct);
        return MissionHandsAccept(MissionHandsStatus.AwaitingToolConfirmation, checkpoint.State.LastSequence);
    }

    public async Task<MissionHandsGrainResult> CancelMissionHandsAttemptAsync(MissionHandsJsonInput input)
    {
        var ct = CancellationToken.None;
        await RepairPendingTransitionIfAnyAsync(ct);
        var request = JsonSerializer.Deserialize(input.Json, ConversationContractsJsonContext.Default.CancelMissionHandsAttemptRequest);
        var attachment = DeserializeMissionHands(checkpoint.State.MissionHandsAttachmentJson,
            ConversationContractsJsonContext.Default.MissionHandsAttachment);
        var outstanding = DeserializeMissionHands(checkpoint.State.MissionHandsRequestJson,
            ConversationContractsJsonContext.Default.MissionToolRequest);
        if (!Exists || request is null || request.ConversationId != Address.ConversationId || string.IsNullOrWhiteSpace(request.Reason) ||
            attachment?.AttachmentId != request.AttachmentId)
            return MissionHandsReject("The mission hands cancellation is stale, foreign, or already terminal.");
        if (checkpoint.State.MissionHandsCancelled)
            return checkpoint.State.MissionHandsCompletedToolRequestId == request.ToolRequestId
                ? MissionHandsAccept(MissionHandsStatus.Cancelled, checkpoint.State.MissionHandsResultSequence)
                : MissionHandsReject("The mission hands cancellation is stale, foreign, or already terminal.");
        if (outstanding?.ToolRequestId != request.ToolRequestId || outstanding.TurnAttemptId != request.TurnAttemptId)
            return MissionHandsReject("The mission hands cancellation is stale, foreign, or already terminal.");

        var cancellation = new SubmitMissionToolResultRequest(Address.ConversationId, request.AttachmentId, request.TurnAttemptId,
            ConversationDeterministicIds.MissionHandsCancellation(request.ToolRequestId), request.ToolRequestId,
            MissionToolOutcome.Cancelled, null, request.Reason);
        checkpoint.State.MissionHandsRequestJson = null;
        checkpoint.State.MissionHandsAwaitingToolConfirmation = false;
        checkpoint.State.MissionHandsInFlight = false;
        checkpoint.State.MissionHandsCancelled = true;
        checkpoint.State.MissionHandsCompletedToolRequestId = request.ToolRequestId;
        checkpoint.State.MissionHandsCompletedCommandId = cancellation.CommandId;
        checkpoint.State.MissionHandsCompletedResultJson = JsonSerializer.Serialize(cancellation,
            ConversationContractsJsonContext.Default.SubmitMissionToolResultRequest);
        var fact = new ConversationEvent(cancellation.CommandId, 1, Address.ConversationId, checkpoint.State.ActiveRunId,
            checkpoint.State.LastSequence + 1, ConversationEventKind.MissionHandsCancelled, ConversationParticipant.Forge,
            null, null, request.Reason, null, null, null, null, null, DateTimeOffset.UtcNow, MissionHandsOutcome: MissionToolOutcome.Cancelled);
        await PlanAppendAdvanceAsync(fact, null, notifyMissionRun: false, dispatchCommand: null, ct);
        checkpoint.State.MissionHandsResultSequence = checkpoint.State.LastSequence;
        await checkpoint.WriteStateAsync();
        return MissionHandsAccept(MissionHandsStatus.Cancelled, checkpoint.State.LastSequence);
    }

    /// <summary>Terminalizes unknown in-flight work after a fresh attachment has reconnected but
    /// cannot prove the old Bob drained. Unlike a clean detach, this never redelivers the request:
    /// a possibly executed operation is recorded once as interrupted and any old late result is
    /// conflict/no-op.</summary>
    public async Task<MissionHandsGrainResult> RecoverMissionHandsInFlightAsync(MissionHandsJsonInput input)
    {
        var ct = CancellationToken.None;
        await RepairPendingTransitionIfAnyAsync(ct);
        var recovery = JsonSerializer.Deserialize(input.Json, ConversationContractsJsonContext.Default.RecoverMissionHandsInFlightRequest);
        var activeAttachment = DeserializeMissionHands(checkpoint.State.MissionHandsAttachmentJson,
            ConversationContractsJsonContext.Default.MissionHandsAttachment);
        var pendingAttachment = DeserializeMissionHands(checkpoint.State.MissionHandsPendingAttachmentJson,
            ConversationContractsJsonContext.Default.MissionHandsAttachment);
        var outstanding = DeserializeMissionHands(checkpoint.State.MissionHandsRequestJson,
            ConversationContractsJsonContext.Default.MissionToolRequest);
        if (!Exists || recovery is null || recovery.ConversationId != Address.ConversationId)
            return MissionHandsReject("The mission hands recovery attachment is stale or foreign.");
        if (checkpoint.State.MissionHandsInterrupted && activeAttachment?.AttachmentId == recovery.AttachmentId &&
            activeAttachment.ApplicationSessionId == recovery.ApplicationSessionId)
            return MissionHandsAccept(MissionHandsStatus.Interrupted, checkpoint.State.MissionHandsResultSequence);
        if (pendingAttachment?.AttachmentId != recovery.AttachmentId ||
            pendingAttachment.ApplicationSessionId != recovery.ApplicationSessionId)
            return MissionHandsReject("The mission hands recovery attachment is stale or foreign.");
        if (!checkpoint.State.MissionHandsInFlight || outstanding is null)
            return MissionHandsReject("There is no unknown in-flight mission hands request to recover.");

        const string reason = "Mission hands execution was interrupted before Host could prove the prior Bob stopped.";
        var terminal = new SubmitMissionToolResultRequest(Address.ConversationId, recovery.AttachmentId,
            outstanding.TurnAttemptId, ConversationDeterministicIds.MissionHandsInterruption(outstanding.ToolRequestId),
            outstanding.ToolRequestId, MissionToolOutcome.Interrupted, null, reason);
        var fact = new ConversationEvent(terminal.CommandId, 1, Address.ConversationId, checkpoint.State.ActiveRunId,
            checkpoint.State.LastSequence + 1, ConversationEventKind.MissionHandsInterrupted, ConversationParticipant.Forge,
            null, null, reason, null, null, null, null, null, DateTimeOffset.UtcNow,
            MissionHandsOutcome: MissionToolOutcome.Interrupted);
        await PlanAppendAdvanceAsync(fact, null, notifyMissionRun: false, dispatchCommand: null, ct);
        // Promote the fresh attachment only after the terminal fact has been ordered. It can
        // observe the outcome and later receive a newly requested operation, never this one.
        checkpoint.State.MissionHandsAttachmentJson = JsonSerializer.Serialize(pendingAttachment,
            ConversationContractsJsonContext.Default.MissionHandsAttachment);
        checkpoint.State.MissionHandsPendingAttachmentJson = null;
        checkpoint.State.MissionHandsRequestJson = null;
        checkpoint.State.MissionHandsAwaitingToolConfirmation = false;
        checkpoint.State.MissionHandsInFlight = false;
        checkpoint.State.MissionHandsCancelled = false;
        checkpoint.State.MissionHandsInterrupted = true;
        checkpoint.State.MissionHandsTerminalReason = reason;
        checkpoint.State.MissionHandsCompletedToolRequestId = outstanding.ToolRequestId;
        checkpoint.State.MissionHandsCompletedCommandId = terminal.CommandId;
        checkpoint.State.MissionHandsCompletedResultJson = JsonSerializer.Serialize(terminal,
            ConversationContractsJsonContext.Default.SubmitMissionToolResultRequest);
        checkpoint.State.MissionHandsResultSequence = checkpoint.State.LastSequence;
        await checkpoint.WriteStateAsync();
        return MissionHandsAccept(MissionHandsStatus.Interrupted, checkpoint.State.LastSequence);
    }

    public async Task<ConversationProgressAcceptance> RecordProgressAsync(ConversationProgressInput input)
    {
        var ct = CancellationToken.None;
        await RepairPendingTransitionIfAnyAsync(ct);

        var progress = JsonSerializer.Deserialize(input.ProgressJson, ConversationContractsJsonContext.Default.ConversationProgress)
            ?? throw new InvalidOperationException("ProgressJson deserialized to null.");

        if (progress.ConversationId != Address.ConversationId)
            return new ConversationProgressAcceptance(
                ConversationProgressOutcome.Rejected, null, "Progress does not match this conversation.");

        // A persisted Project Control transcript remains readable, but after retirement no
        // generic progress command may mutate it. This check is at the Host owner boundary,
        // ahead of idempotency and sequence allocation, so an old queue body cannot revive it.
        if (checkpoint.State.Purpose == ConversationPurpose.ProjectControl)
            return new ConversationProgressAcceptance(
                ConversationProgressOutcome.Rejected, null, "Legacy Project Control is read-only.");

        if (progress.RunId is null || progress.RunId != checkpoint.State.ActiveRunId)
        {
            return new ConversationProgressAcceptance(
                ConversationProgressOutcome.Rejected, null, "Progress does not match this conversation's active run.");
        }

        // A Core root pause is represented as a Worker progress fact, but only the Host turns it
        // into the canonical MissionHandsRequested/AwaitingHands sequence. The Worker supplies
        // opaque continuation content; it cannot sequence, attach Bob, or dispatch a result.
        if (progress.Kind == ConversationEventKind.MissionHandsRequested)
        {
            if (progress.MissionHandsRequest is null || progress.EventId != progress.MissionHandsRequest.ToolRequestId)
                return new ConversationProgressAcceptance(ConversationProgressOutcome.Rejected, null, "Invalid generic mission hands request.");
            var activeStart = checkpoint.State.ActiveStartCommandJson is { } activeStartJson
                ? JsonSerializer.Deserialize(activeStartJson, ConversationContractsJsonContext.Default.ConversationCommand)
                : null;
            var pinnedLaunch = DeserializeMissionHands(checkpoint.State.MissionHandsLaunchJson,
                ConversationContractsJsonContext.Default.DurableMissionLaunch);
            if (checkpoint.State.MissionHandsExpectedCommandId != progress.MissionHandsRequest.TurnAttemptId ||
                activeStart?.Launch is null || pinnedLaunch is null || !DurableMissionLaunchComparison.SameLaunch(activeStart.Launch, pinnedLaunch))
                return new ConversationProgressAcceptance(ConversationProgressOutcome.Rejected, null,
                    "The mission hands request is not correlated to the current Host-dispatched generic command.");
            var hands = await RecordMissionHandsRequestAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
                progress.MissionHandsRequest, ConversationContractsJsonContext.Default.MissionToolRequest)));
            return hands.Accepted
                ? new ConversationProgressAcceptance(ConversationProgressOutcome.Appended, checkpoint.State.LastSequence, null)
                : new ConversationProgressAcceptance(ConversationProgressOutcome.Rejected, null, "Mission hands request was rejected.");
        }

        if (progress.Kind == ConversationEventKind.ToolResult)
        {
            var expected = checkpoint.State.ExpectedToolRequestId;
            if (expected is null || progress.ToolResult is null || progress.ToolResult.RequestId != expected.Value)
                return new ConversationProgressAcceptance(
                    ConversationProgressOutcome.Rejected, null,
                    "Tool result does not match the expected outstanding tool request.");
        }

        var planned = new ConversationEvent(
            progress.EventId, 1, progress.ConversationId, progress.RunId, checkpoint.State.LastSequence + 1,
            progress.Kind, progress.Participant, progress.Attempt, progress.Text, progress.Reason,
            progress.Approval, progress.ToolRequest, progress.ToolResult, progress.Artifact, progress.RunStatus,
            progress.OccurredAtUtc, MissionHandsRequest: progress.MissionHandsRequest);

        var existing = await eventStore.FindByEventIdAsync(Address, progress.EventId, ct);
        if (existing is not null)
        {
            var existingJson = JsonSerializer.Serialize(existing.Event, ConversationContractsJsonContext.Default.ConversationEvent);
            var plannedAtExistingSequence = JsonSerializer.Serialize(
                planned with { Sequence = existing.Event.Sequence }, ConversationContractsJsonContext.Default.ConversationEvent);

            return string.Equals(existingJson, plannedAtExistingSequence, StringComparison.Ordinal)
                ? new ConversationProgressAcceptance(ConversationProgressOutcome.AlreadyRecorded, existing.Event.Sequence, null)
                : new ConversationProgressAcceptance(
                    ConversationProgressOutcome.Rejected, null, "Event ID already recorded with different content.");
        }

        // A valid ToolResult (already matched against ExpectedToolRequestId above) deterministically
        // owes a ContinueAfterTool dispatch derived from the active run's own StartMission command —
        // never a fresh Guid, so a repaired/retried transition always re-derives the identical
        // continuation command.
        ConversationCommand? dispatchCommand = null;
        if (progress.Kind == ConversationEventKind.ToolResult)
        {
            var startCommand = JsonSerializer.Deserialize(
                checkpoint.State.ActiveStartCommandJson
                    ?? throw new InvalidOperationException("No ActiveStartCommandJson for a conversation with an outstanding tool result."),
                ConversationContractsJsonContext.Default.ConversationCommand)
                ?? throw new InvalidOperationException("ActiveStartCommandJson deserialized to null.");

            dispatchCommand = startCommand with
            {
                CommandId = ConversationDeterministicIds.Continuation(progress.EventId),
                Kind = ConversationCommandKind.ContinueAfterTool,
                ToolResult = progress.ToolResult,
            };
        }

        var stored = await PlanAppendAdvanceAsync(planned, null, notifyMissionRun: true, dispatchCommand, ct);
        return new ConversationProgressAcceptance(ConversationProgressOutcome.Appended, stored.Sequence, null);
    }

    public async Task RecordRunInterruptionAsync(MissionRunInterruption interruption)
    {
        var ct = CancellationToken.None;
        await RepairPendingTransitionIfAnyAsync(ct);

        var existing = await eventStore.FindByEventIdAsync(Address, interruption.EventId, ct);
        if (existing is not null)
            return; // Idempotent retry of an already-durable interruption report.

        var planned = new ConversationEvent(
            interruption.EventId, 1, Address.ConversationId, interruption.RunId, checkpoint.State.LastSequence + 1,
            ConversationEventKind.RunStatus, ConversationParticipant.Forge, null, null, null,
            null, null, null, null, ConversationRunStatus.Interrupted, interruption.OccurredAtUtc);

        // Deliberately notifyMissionRun: false — MissionRunGrain already persisted its own
        // Interrupted/Terminal state before calling this; notifying it back here would be a
        // synchronous re-entrant call into its still-executing activation.
        await PlanAppendAdvanceAsync(planned, null, notifyMissionRun: false, dispatchCommand: null, ct);
    }

    public Task<ConversationSnapshotResult> GetSnapshotAsync()
    {
        // MissionRef is projected as NULL when unset rather than as the empty string the checkpoint
        // initialises it to: a Project Mission container genuinely has no mission, and the snapshot
        // must say so rather than offer an empty sentinel a caller has to interpret.
        var snapshot = new ConversationSnapshot(
            Address.ConversationId,
            string.IsNullOrEmpty(checkpoint.State.MissionRef) ? null : checkpoint.State.MissionRef,
            checkpoint.State.ActiveRunId,
            checkpoint.State.LastSequence, checkpoint.State.Status, checkpoint.State.ExpectedToolRequestId,
            checkpoint.State.UpdatedAtUtc, checkpoint.State.Purpose, checkpoint.State.ProjectId,
            DeserializeMissionHands(checkpoint.State.MissionConversationLaunchJson,
                ConversationContractsJsonContext.Default.DurableMissionLaunch), checkpoint.State.EvaluationResultId,
            checkpoint.State.EvaluationTurnId, checkpoint.State.EvaluationTurnAttemptId,
            checkpoint.State.EvaluationTerminalSummary, checkpoint.State.EvaluationTerminalReason);

        return Task.FromResult(new ConversationSnapshotResult(
            JsonSerializer.Serialize(snapshot, ConversationContractsJsonContext.Default.ConversationSnapshot)));
    }

    public async Task<ConversationEventBatch> ReadAfterAsync(long sequence)
    {
        var events = new List<string>();
        await foreach (var evt in eventStore.ReadAfterAsync(Address, sequence, CancellationToken.None))
            events.Add(JsonSerializer.Serialize(evt, ConversationContractsJsonContext.Default.ConversationEvent));

        return new ConversationEventBatch([.. events]);
    }

    public async Task<ConversationProjectReadResult> ReadProjectRunsAsync(long? anchor, long? before)
    {
        if (!IsProjectMissionContainer())
            return ProjectError("wrongPurpose", "This conversation is not a Project Mission container.");
        if (!ValidCursor(anchor, before, checkpoint.State.LastSequence))
            return ProjectError("invalidRequest", "The runs cursor is invalid.");
        try
        {
            var target = checkpoint.State.LastSequence;
            var current = await ProjectRuns.AdvanceOnceAsync(Address, target, CancellationToken.None);
            if (anchor is { } requestedAnchor && requestedAnchor > current.IndexedSequence)
                return ProjectError("invalidRequest", "The runs cursor is ahead of indexed history.");
            var pageAnchor = anchor ?? current.IndexedSequence;
            var summaries = await ProjectRuns.ReadPageAsync(Address, pageAnchor, before, CancellationToken.None);
            var visible = summaries.Take(20).ToArray();
            var next = summaries.Length > 20 && visible.Length > 0 ? new ProjectRunCursor(pageAnchor, visible[^1].AcceptedSequence) : null;
            return ProjectPayload(
                new ProjectRunPage(Address.ConversationId, current.IndexedSequence, target, current.IndexedSequence < target, visible, next),
                ConversationContractsJsonContext.Default.ProjectRunPage);
        }
        catch (ProjectHistoryInvalidException ex)
        {
            return ProjectError("historyInvalid", ex.Message);
        }
    }

    public async Task<ConversationProjectReadResult> ReadProjectRunAsync(Guid runId)
    {
        if (runId == Guid.Empty)
            return ProjectError("invalidRequest", "A run id is required.");
        if (!IsProjectMissionContainer())
            return ProjectError("wrongPurpose", "This conversation is not a Project Mission container.");
        try
        {
            var target = checkpoint.State.LastSequence;
            var current = await ProjectRuns.AdvanceOnceAsync(Address, target, CancellationToken.None);
            var summary = await ProjectRuns.FindAsync(Address, runId, CancellationToken.None);
            if (summary is null)
                return MissingRun(current, target);
            var receipt = await ProjectRuns.FindCommandAsync(Address, summary.CommandId, CancellationToken.None)
                ?? throw new ProjectHistoryInvalidException("Run input is missing its accepted command.");
            return ProjectPayload(
                new ProjectRunDetail(summary, receipt.Input, current.IndexedSequence, target),
                ConversationContractsJsonContext.Default.ProjectRunDetail);
        }
        catch (ProjectHistoryInvalidException ex)
        {
            return ProjectError("historyInvalid", ex.Message);
        }
    }

    public async Task<ConversationProjectReadResult> ReadProjectRunEventsAsync(Guid runId, long after, long? through)
    {
        if (runId == Guid.Empty || after < 0)
            return ProjectError("invalidRequest", "The trace range is invalid.");
        if (!IsProjectMissionContainer())
            return ProjectError("wrongPurpose", "This conversation is not a Project Mission container.");
        var target = through ?? checkpoint.State.LastSequence;
        if (target < after || target > checkpoint.State.LastSequence)
            return ProjectError("invalidRequest", "The trace range is invalid.");
        try
        {
            var current = await ProjectRuns.AdvanceOnceAsync(Address, checkpoint.State.LastSequence, CancellationToken.None);
            if (await ProjectRuns.FindAsync(Address, runId, CancellationToken.None) is null)
                return MissingRun(current, target);
            var page = await ProjectRuns.ReadEventsAsync(Address, runId, after, target, CancellationToken.None);
            return ProjectPayload(page, ConversationContractsJsonContext.Default.ProjectRunEventPage);
        }
        catch (ProjectHistoryInvalidException ex)
        {
            return ProjectError("historyInvalid", ex.Message);
        }
    }

    public async Task<ConversationProjectReadResult> ReadProjectCommandAsync(Guid commandId)
    {
        if (commandId == Guid.Empty)
            return ProjectError("invalidRequest", "A command id is required.");
        if (!IsProjectMissionContainer())
            return ProjectError("wrongPurpose", "This conversation is not a Project Mission container.");
        try
        {
            var receipt = await ProjectRuns.FindCommandAsync(Address, commandId, CancellationToken.None);
            return receipt is null
                ? ProjectError("notFound", "The command was not found.")
                : ProjectPayload(receipt, ConversationContractsJsonContext.Default.ProjectCommandReceipt);
        }
        catch (ProjectHistoryInvalidException ex)
        {
            return ProjectError("historyInvalid", ex.Message);
        }
    }

    private bool IsProjectMissionContainer() => checkpoint.State.Purpose == ConversationPurpose.ProjectMission && checkpoint.State.ProjectId is not null;
    private static bool ValidCursor(long? anchor, long? before, long last) =>
        (anchor is null && before is null) ||
        (anchor is { } a && before is { } b && a > 0 && a <= last && b > 0 && b <= a);
    private static ConversationProjectReadResult ProjectError(string code, string message) => new(null, code, message);
    private static ConversationProjectReadResult ProjectPayload<T>(T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type) => new(JsonSerializer.Serialize(value, type), null, null);
    private static ConversationProjectReadResult MissingRun(ProjectRunIndexCheckpoint current, long target) =>
        current.IndexedSequence < target
            ? ProjectError("historySynchronizing", "Run history is synchronizing; retry shortly.")
            : ProjectError("notFound", "The run was not found.");

    // -- start-pair (UserMessage + paired RunStatus(Queued)) protocol --

    /// <summary>Begins a new run: validates the fixed 32 KiB start-command bound, then preallocates
    /// and durably records the paired Queued event's identity/timestamp in ONE checkpoint write
    /// before either start fact is ever appended, then completes it. <c>PendingRunStart</c> is the
    /// SOLE retained start-command copy during this window — <c>ActiveStartCommandJson</c> is not
    /// also set here, because two full command copies could exceed the Azure Table-backed
    /// Orleans-state cell limit. Shared by <see cref="AcceptCommandAsync"/> (first run) and
    /// <see cref="AcceptFollowupCommandAsync"/> (every later run).</summary>
    private async Task<ConversationCommandOutcomeResult> BeginRunAsync(ConversationCommand command, CancellationToken ct)
    {
        var commandJson = JsonSerializer.Serialize(command, ConversationContractsJsonContext.Default.ConversationCommand);
        var byteCount = Encoding.UTF8.GetByteCount(commandJson);
        if (byteCount > ConversationJsonLimits.MaxStartCommandJsonBytes)
            return new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Invalid, null,
                $"Start command JSON ({byteCount} bytes) exceeds the {ConversationJsonLimits.MaxStartCommandJsonBytes}-byte limit.");

        checkpoint.State.PendingRunStart = new PendingRunStart(commandJson, Guid.NewGuid(), DateTimeOffset.UtcNow);
        await checkpoint.WriteStateAsync();

        var acceptance = await CompletePendingRunStartAsync(ct);
        return new ConversationCommandOutcomeResult(ConversationCommandOutcome.Accepted, acceptance, null);
    }

    private async Task RepairPendingRunStartIfAnyAsync(CancellationToken ct)
    {
        if (checkpoint.State.PendingRunStart is null)
            return;

        await CompletePendingRunStartAsync(ct);
    }

    /// <summary>Resolves/appends the UserMessage by CommandId (idempotent), then resolves/appends
    /// the paired RunStatus(Queued) by the PREALLOCATED QueuedEventId (also idempotent) — if that
    /// event is already durable, its own completed pending transition already proved any owed
    /// dispatch was broker-accepted, so this never resends. Because the Queued event is always
    /// allocated as the very next sequence after the UserMessage's own advance, in a single-threaded
    /// grain activation where nothing else can interleave, the paired <c>n + 1</c> relationship
    /// holds structurally, not just by convention.</summary>
    private async Task<ConversationCommandAcceptance> CompletePendingRunStartAsync(CancellationToken ct)
    {
        var pendingStart = checkpoint.State.PendingRunStart
            ?? throw new InvalidOperationException("CompletePendingRunStartAsync called with no PendingRunStart.");

        var command = JsonSerializer.Deserialize(pendingStart.StartCommandJson, ConversationContractsJsonContext.Default.ConversationCommand)
            ?? throw new InvalidOperationException("PendingRunStart.StartCommandJson deserialized to null.");

        var storedUser = await eventStore.FindByEventIdAsync(Address, command.CommandId, ct);
        ConversationEvent userEvent;
        if (storedUser is null)
        {
            var userMessage = new ConversationEvent(
                command.CommandId, 1, Address.ConversationId, command.RunId, checkpoint.State.LastSequence + 1,
                ConversationEventKind.UserMessage, ConversationParticipant.User, null, command.Goal, null,
                null, null, null, null, null, DateTimeOffset.UtcNow);
            userEvent = await PlanAppendAdvanceAsync(userMessage, command, notifyMissionRun: true, dispatchCommand: null, ct);
        }
        else
        {
            userEvent = storedUser.Event;
        }

        var storedQueued = await eventStore.FindByEventIdAsync(Address, pendingStart.QueuedEventId, ct);
        ConversationEvent queuedEvent;
        if (storedQueued is null)
        {
            var queued = new ConversationEvent(
                pendingStart.QueuedEventId, 1, Address.ConversationId, command.RunId, checkpoint.State.LastSequence + 1,
                ConversationEventKind.RunStatus, ConversationParticipant.Forge, null, null, null,
                null, null, null, null, ConversationRunStatus.Queued, pendingStart.QueuedOccurredAtUtc);
            queuedEvent = await PlanAppendAdvanceAsync(queued, null, notifyMissionRun: true, dispatchCommand: command, ct);
        }
        else
        {
            queuedEvent = storedQueued.Event;
        }

        // Only now that the queued transition is durably present — its own completed pending
        // transition already proves any owed dispatch was broker-accepted — retain the start
        // command as the active run's copy and release PendingRunStart, in ONE checkpoint write.
        checkpoint.State.ActiveStartCommandJson = pendingStart.StartCommandJson;
        checkpoint.State.PendingRunStart = null;
        checkpoint.State.MissionHandsExpectedCommandId = command.Launch?.Package is null ? null : command.CommandId;
        await checkpoint.WriteStateAsync();

        return new ConversationCommandAcceptance(Address.ConversationId, command.RunId, queuedEvent.Sequence,
            ConversationRunStatus.Queued, command.TurnId);
    }

    /// <summary>Finds the Host-owned accepted command for one durable turn by replaying canonical
    /// events. This is deliberately a query over the existing event log, not a second transcript
    /// or a caller-owned turn map.</summary>
    private async Task<ConversationCommand?> FindMissionTurnAsync(Guid turnId, CancellationToken ct)
    {
        await foreach (var item in eventStore.ReadAfterAsync(Address, 0, ct))
        {
            if (item.Kind != ConversationEventKind.UserMessage)
                continue;
            var stored = await eventStore.FindByEventIdAsync(Address, item.EventId, ct);
            var command = stored?.AcceptedCommandJson is null ? null : JsonSerializer.Deserialize(
                stored.AcceptedCommandJson, ConversationContractsJsonContext.Default.ConversationCommand);
            if (command?.TurnId == turnId)
                return command;
        }

        return null;
    }

    /// <summary>Terminal cancellation is a durable no-op. The caller receives the already-stored
    /// status only when both the turn and attempt are the canonical pair.</summary>
    private async Task<ConversationEvent?> ReadTerminalMissionTurnAsync(Guid turnId, Guid attemptId, CancellationToken ct)
    {
        var matched = false;
        await foreach (var item in eventStore.ReadAfterAsync(Address, 0, ct))
        {
            if (item.Kind != ConversationEventKind.UserMessage || item.RunId != attemptId)
                continue;
            var stored = await eventStore.FindByEventIdAsync(Address, item.EventId, ct);
            var command = stored?.AcceptedCommandJson is null ? null : JsonSerializer.Deserialize(
                stored.AcceptedCommandJson, ConversationContractsJsonContext.Default.ConversationCommand);
            if (command?.TurnId == turnId)
            {
                matched = true;
                break;
            }
        }
        if (!matched) return null;
        var latest = await eventStore.ReadLatestForRunAsync(Address, attemptId, ct);
        return latest is { RunStatus: { } status } && IsTerminal(status) ? latest : null;
    }

    // -- explicit duplicate equality (Task 6) — never relies on AppendAsync's own equality-throw --

    private ConversationCommandOutcomeResult ResolveDuplicateStart(StoredConversationEvent existing, ConversationCommand command)
    {
        var reconstructedCommandJson =
            JsonSerializer.Serialize(command, ConversationContractsJsonContext.Default.ConversationCommand);

        var isEqual =
            existing.Event.ConversationId == command.ConversationId &&
            existing.Event.RunId == command.RunId &&
            existing.Event.Kind == ConversationEventKind.UserMessage &&
            existing.Event.Participant == ConversationParticipant.User &&
            existing.Event.Text == command.Goal &&
            existing.AcceptedCommandJson == reconstructedCommandJson;

        return isEqual
            ? new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Accepted,
                new ConversationCommandAcceptance(
                    Address.ConversationId, existing.Event.RunId!.Value, existing.Event.Sequence + 1, ConversationRunStatus.Queued),
                null)
            : new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Conflict, null, "CommandId already used with different content.");
    }

    private ConversationCommandOutcomeResult ResolveDuplicateFollowup(StoredConversationEvent existing, ConversationFollowupCommandInput input)
    {
        var isEqual =
            existing.Event.ConversationId == Address.ConversationId &&
            existing.Event.Kind == ConversationEventKind.UserMessage &&
            existing.Event.Participant == ConversationParticipant.User &&
            existing.Event.Text == input.Text;

        return isEqual
            ? new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Accepted,
                new ConversationCommandAcceptance(
                    Address.ConversationId, existing.Event.RunId!.Value, existing.Event.Sequence + 1, ConversationRunStatus.Queued),
                null)
            : new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Conflict, null, "CommandId already used with different content.");
    }

    private ConversationCommandOutcomeResult ResolveDuplicateToolResult(StoredConversationEvent existing, ConversationToolResultInput input)
    {
        var isEqual =
            existing.Event.ConversationId == Address.ConversationId &&
            existing.Event.Kind == ConversationEventKind.ToolResult &&
            existing.Event.Participant == ConversationParticipant.Implementer &&
            existing.Event.ToolResult is { } tr &&
            tr.RequestId == input.ToolRequestId && tr.Content == input.Content && tr.IsError == input.IsError;

        return isEqual
            ? new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Accepted,
                new ConversationCommandAcceptance(
                    Address.ConversationId, existing.Event.RunId!.Value, existing.Event.Sequence, ConversationRunStatus.WaitingForTool),
                null)
            : new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Conflict, null, "CommandId already used with different content.");
    }

    // -- shared protocol steps --

    private async Task RepairPendingTransitionIfAnyAsync(CancellationToken ct)
    {
        if (checkpoint.State.PendingTransition is not { } pending)
            return;

        var plannedEvent = JsonSerializer.Deserialize(pending.PlannedEventJson, ConversationContractsJsonContext.Default.ConversationEvent)
            ?? throw new InvalidOperationException("Pending PlannedEventJson deserialized to null.");

        var stored = await eventStore.AppendAsync(Address, plannedEvent, pending.AcceptedCommandJson, ct);
        // Reproduce the original call's own notification intent — in particular, a repaired
        // interruption-report transition must stay notifyMissionRun: false, never defaulting to
        // true, or it would call back into MissionRunGrain in violation of the no-cycle rule.
        await AdvanceAsync(stored, pending.NotifyMissionRun);
        await CompleteDispatchAndClearAsync(ct);
    }

    private async Task<ConversationEvent> PlanAppendAdvanceAsync(
        ConversationEvent plannedEvent, ConversationCommand? acceptedCommand, bool notifyMissionRun,
        ConversationCommand? dispatchCommand, CancellationToken ct)
    {
        var plannedJson = JsonSerializer.Serialize(plannedEvent, ConversationContractsJsonContext.Default.ConversationEvent);
        var commandJson = acceptedCommand is null
            ? null
            : JsonSerializer.Serialize(acceptedCommand, ConversationContractsJsonContext.Default.ConversationCommand);
        var dispatchJson = dispatchCommand is null
            ? null
            : JsonSerializer.Serialize(dispatchCommand, ConversationContractsJsonContext.Default.ConversationCommand);

        // Registered BEFORE the transition is persisted: once durable state says a dispatch is
        // owed, a reminder must already exist to guarantee that owed send is retried even if this
        // activation never repairs it itself.
        if (dispatchCommand is not null)
            await this.RegisterOrUpdateReminder(OutboxReminderName, OutboxReminderDueTime, OutboxReminderPeriod);

        checkpoint.State.PendingTransition =
            new PendingConversationTransition(plannedJson, commandJson, DispatchState.NotDispatched, notifyMissionRun, dispatchJson);
        await checkpoint.WriteStateAsync();

        var stored = await eventStore.AppendAsync(Address, plannedEvent, commandJson, ct);
        await AdvanceAsync(stored, notifyMissionRun);
        await CompleteDispatchAndClearAsync(ct);
        return stored;
    }

    // The event-append/snapshot/notify half of one transition. PendingTransition is deliberately
    // NOT cleared here — CompleteDispatchAndClearAsync clears it only once any owed dispatch has
    // been sent and broker-accepted, so a crash between these two steps always has a durable
    // record of exactly what is still owed. AppendAsync is idempotent and ApplyDurableEventAsync's
    // effect is a pure function of (Kind, RunStatus), so re-running this method is always safe.
    private async Task AdvanceAsync(ConversationEvent stored, bool notifyMissionRun)
    {
        checkpoint.State.LastSequence = Math.Max(checkpoint.State.LastSequence, stored.Sequence);
        ApplySnapshotFields(stored);
        checkpoint.State.UpdatedAtUtc = stored.OccurredAtUtc;
        await checkpoint.WriteStateAsync();

        // Published only after the checkpoint write above is durable, and only once per call —
        // including on repair, which can therefore emit a harmless duplicate live notification for
        // an event the client may have already rendered; its event ID/sequence makes that safe.
        notifier.Publish(Address, stored);

        if (notifyMissionRun && stored.RunId is { } runId)
        {
            var runGrain = grainFactory.GetGrain<IMissionRunGrain>(MissionRunGrainKey(checkpoint.State.TenantId, runId));
            await runGrain.ApplyDurableEventAsync(
                new MissionRunEventInput(stored.EventId, runId, stored.ConversationId, stored.Kind, stored.RunStatus));
        }
    }

    // Sends the owed dispatch (a resend when DispatchState is still NotDispatched is safe — the
    // queue dedupes on MessageId), persists BrokerAccepted so recovery never resends after this,
    // then clears PendingTransition and unregisters the outbox reminder only once that clear is
    // durable.
    private async Task CompleteDispatchAndClearAsync(CancellationToken ct)
    {
        var pending = checkpoint.State.PendingTransition;
        if (pending is null)
            return;

        var owesDispatch = pending.DispatchCommandJson is not null;

        if (pending.DispatchCommandJson is { } dispatchJson && pending.DispatchState == DispatchState.NotDispatched)
        {
            var dispatchCommand = JsonSerializer.Deserialize(dispatchJson, ConversationContractsJsonContext.Default.ConversationCommand)
                ?? throw new InvalidOperationException("Pending DispatchCommandJson deserialized to null.");

            await dispatcher.SendAsync(Address, dispatchCommand, ct);

            checkpoint.State.PendingTransition = pending with { DispatchState = DispatchState.BrokerAccepted };
            await checkpoint.WriteStateAsync();
        }

        checkpoint.State.PendingTransition = null;
        await checkpoint.WriteStateAsync();

        if (owesDispatch)
        {
            var reminder = await this.GetReminder(OutboxReminderName);
            if (reminder is not null)
                await this.UnregisterReminder(reminder);
        }
    }

    private void ApplySnapshotFields(ConversationEvent stored)
    {
        switch (stored.Kind)
        {
            case ConversationEventKind.RunStatus:
                checkpoint.State.Status = stored.RunStatus!.Value;
                var terminal = IsTerminal(stored.RunStatus.Value);
                checkpoint.State.ActiveRunId = terminal ? null : stored.RunId;
                if (terminal)
                    checkpoint.State.ActiveStartCommandJson = null;
                break;
            case ConversationEventKind.ParticipantMessage when checkpoint.State.Purpose == ConversationPurpose.Evaluation &&
                stored.RunId == checkpoint.State.EvaluationTurnAttemptId:
                checkpoint.State.EvaluationTerminalSummary = stored.Text;
                break;
            case ConversationEventKind.Error when checkpoint.State.Purpose == ConversationPurpose.Evaluation &&
                stored.RunId == checkpoint.State.EvaluationTurnAttemptId:
                checkpoint.State.EvaluationTerminalReason = stored.Reason;
                break;
            case ConversationEventKind.ToolRequested:
                checkpoint.State.ExpectedToolRequestId = stored.ToolRequest?.RequestId;
                checkpoint.State.Status = ConversationRunStatus.WaitingForTool;
                break;
            case ConversationEventKind.ToolResult:
                checkpoint.State.ExpectedToolRequestId = null;
                break;
        }
    }

    private static bool IsTerminal(ConversationRunStatus status) => status is
        ConversationRunStatus.Completed or ConversationRunStatus.Rejected or
        ConversationRunStatus.Interrupted or ConversationRunStatus.Failed;

    private bool Exists => !string.IsNullOrEmpty(checkpoint.State.MissionRef) ||
        (checkpoint.State.Purpose is ConversationPurpose.ProjectMission or ConversationPurpose.MissionConversation or ConversationPurpose.Evaluation &&
         checkpoint.State.ProjectId is not null);

    private static bool ValidMissionHandsLaunch(DurableMissionLaunch launch)
    {
        if (launch.MissionVersionId == Guid.Empty || launch.VersionNumber <= 0 ||
            string.IsNullOrWhiteSpace(launch.Definition) || launch.Definition.Length > 256 * 1024 || !Enum.IsDefined(launch.Profile))
            return false;
        var expected = "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(launch.Definition))).ToLowerInvariant();
        return string.Equals(expected, launch.DefinitionHash, StringComparison.Ordinal) &&
            // Schema-4 pre-generic launches remain attachable for historic read/reconnect. Any
            // package-bearing attachment is independently parsed before Host persists it.
            (launch.Package is null || DurableMissionPackageAdmission.TryValidate(launch, out _));
    }

    private MissionHandsGrainResult MissionHandsAccept(MissionHandsStatus status, long? sequence) => new(
        JsonSerializer.Serialize(new MissionHandsResult(status, sequence), ConversationContractsJsonContext.Default.MissionHandsResult), true);

    private MissionHandsGrainResult MissionHandsReject(string reason) => new(
        JsonSerializer.Serialize(new MissionHandsResult(MissionHandsStatus.AwaitingHands, null, reason),
            ConversationContractsJsonContext.Default.MissionHandsResult), false);

    private MissionHandsGrainResult MissionHandsWork(MissionHandsStatus status, MissionHandsAttachment attachment,
        MissionToolRequest? request, long sequence, string? reason = null) => new(
        JsonSerializer.Serialize(new MissionHandsWorkItem(status, attachment, request, sequence, reason),
            ConversationContractsJsonContext.Default.MissionHandsWorkItem), true);

    private MissionHandsGrainResult MissionHandsWorkReject(string reason) => new(
        JsonSerializer.Serialize(new MissionHandsWorkItem(MissionHandsStatus.AwaitingHands, null, null, null, reason),
            ConversationContractsJsonContext.Default.MissionHandsWorkItem), false);

    private static T? DeserializeMissionHands<T>(string? json,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type) where T : class =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize(json, type);

    private static string MissionRunGrainKey(string tenantId, Guid runId) => $"{tenantId}|{runId:N}";
}
