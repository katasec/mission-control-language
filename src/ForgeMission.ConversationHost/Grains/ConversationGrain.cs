using System.Text;
using System.Text.Json;
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
    IGrainFactory grainFactory,
    IConversationCommandDispatcher dispatcher,
    IConversationEventNotifier notifier)
    : Grain, IConversationGrain, IRemindable
{
    private const string OutboxReminderName = "mission-command-outbox";

    /// <summary>The fixed resolver key a Project-control command carries. Grain-set, never
    /// caller-supplied, so a control message can never select Janus.</summary>
    private const string ProjectControlMissionRef = "MissionControl";

    private static readonly TimeSpan OutboxReminderDueTime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan OutboxReminderPeriod = TimeSpan.FromMinutes(1);

    private ConversationAddress Address => ConversationAddress.Parse(this.GetPrimaryKeyString());

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
                "This conversation is a Project-control conversation; it cannot start a mission run.");

        // A MissionRun command names a run and carries no project goal. Both are guaranteed by the
        // HTTP adapter (StartConversationRequest has neither field), so these guard any OTHER
        // caller of this grain rather than a live route.
        if (command.RunId is null)
            return new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Invalid, null, "A mission-run command requires a run id.");

        if (command.ProjectGoal is not null)
            return new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Invalid, null,
                "A mission-run command cannot carry a project goal; that value is Project-control state.");

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
                "This conversation is a Project-control conversation; it cannot start a mission run.");

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

    // -- Project-control acceptance (43.20 task 2) --

    public async Task<ConversationCommandOutcomeResult> AcceptControlCreateAsync(ConversationControlCreateInput input)
    {
        var ct = CancellationToken.None;
        await RepairPendingTransitionIfAnyAsync(ct);

        if (input.ProjectId == Guid.Empty || string.IsNullOrWhiteSpace(input.ProjectGoal))
            return new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Invalid, null, "A Project-control create requires a project id and a non-empty goal.");

        // Already pinned: recognise an exact retry by the create command's own CONTENT, mirroring
        // ResolveDuplicateStart. A create naming a different Project or goal must never silently
        // repoint an existing control conversation.
        if (!string.IsNullOrEmpty(checkpoint.State.MissionRef))
        {
            var isSameControlConversation =
                checkpoint.State.Purpose == ConversationPurpose.ProjectControl &&
                checkpoint.State.ProjectId == input.ProjectId &&
                string.Equals(checkpoint.State.ProjectGoal, input.ProjectGoal, StringComparison.Ordinal);

            return isSameControlConversation
                ? Accepted(checkpoint.State.LastSequence)
                : new ConversationCommandOutcomeResult(
                    ConversationCommandOutcome.Conflict, null,
                    "This conversation is already pinned to a different purpose, Project, or goal.");
        }

        // Pinning MissionRef is what keeps the existing snapshot-based existence check working
        // unchanged for a control conversation: a created one is found, a never-created one is not.
        checkpoint.State.TenantId = Address.TenantId;
        checkpoint.State.ConversationId = Address.ConversationId;
        checkpoint.State.MissionRef = ProjectControlMissionRef;
        checkpoint.State.Purpose = ConversationPurpose.ProjectControl;
        checkpoint.State.ProjectId = input.ProjectId;
        checkpoint.State.ProjectGoal = input.ProjectGoal;
        checkpoint.State.PinnedCapabilitiesJson = "[]";
        checkpoint.State.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await checkpoint.WriteStateAsync();

        // Deliberately appends NO event: a newly created control conversation is empty, so its
        // accepted sequence is the current LastSequence (0). There is nothing to dispatch and no
        // run to begin, which is why this path needs neither the outbox nor PendingRunStart.
        return Accepted(checkpoint.State.LastSequence);

        ConversationCommandOutcomeResult Accepted(long sequence) =>
            new(ConversationCommandOutcome.Accepted,
                new ConversationCommandAcceptance(Address.ConversationId, null, sequence, checkpoint.State.Status),
                null);
    }

    public async Task<ConversationCommandOutcomeResult> AcceptControlMessageAsync(ConversationControlMessageInput input)
    {
        var ct = CancellationToken.None;
        await RepairPendingTransitionIfAnyAsync(ct);

        if (checkpoint.State.Purpose != ConversationPurpose.ProjectControl)
            return new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Conflict, null, "This conversation is not a Project-control conversation.");

        var existing = await eventStore.FindByEventIdAsync(Address, input.CommandId, ct);
        if (existing is not null)
            return ResolveDuplicateControlMessage(existing, input);

        // ProjectGoal is read from pinned checkpoint state and from nowhere else — the input has
        // no such member, so no expression here can source it from the caller.
        var command = new ConversationCommand(
            input.CommandId, Address.ConversationId, null, ConversationCommandKind.StartMission,
            ProjectControlMissionRef, input.Text, [], null, checkpoint.State.ProjectGoal);

        var commandJson = JsonSerializer.Serialize(command, ConversationContractsJsonContext.Default.ConversationCommand);
        var byteCount = Encoding.UTF8.GetByteCount(commandJson);
        if (byteCount > ConversationJsonLimits.MaxStartCommandJsonBytes)
            return new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Invalid, null,
                $"Control command JSON ({byteCount} bytes) exceeds the {ConversationJsonLimits.MaxStartCommandJsonBytes}-byte limit.");

        var planned = new ConversationEvent(
            input.CommandId, 1, Address.ConversationId, null, checkpoint.State.LastSequence + 1,
            ConversationEventKind.UserMessage, ConversationParticipant.User, null, input.Text, null,
            null, null, null, null, null, DateTimeOffset.UtcNow);

        // ONE event and ONE dispatch through the existing pending-transition/outbox protocol.
        // notifyMissionRun: false is belt-and-braces — AdvanceAsync already cannot notify for a
        // null-run event — but persisting the intent keeps a repaired transition truthful.
        var stored = await PlanAppendAdvanceAsync(planned, command, notifyMissionRun: false, command, ct);

        return new ConversationCommandOutcomeResult(
            ConversationCommandOutcome.Accepted,
            new ConversationCommandAcceptance(Address.ConversationId, null, stored.Sequence, checkpoint.State.Status),
            null);
    }

    public async Task<ConversationCommandOutcomeResult> AcceptToolResultAsync(ConversationToolResultInput input)
    {
        var ct = CancellationToken.None;
        await RepairPendingTransitionIfAnyAsync(ct);

        // Resolved BEFORE checking current run state — an exact replay of an already-accepted tool
        // result must return its original acceptance even after the run has since gone terminal and
        // ExpectedToolRequestId/ActiveRunId no longer reflect it.
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

    public async Task<ConversationProgressAcceptance> RecordProgressAsync(ConversationProgressInput input)
    {
        var ct = CancellationToken.None;
        await RepairPendingTransitionIfAnyAsync(ct);

        var progress = JsonSerializer.Deserialize(input.ProgressJson, ConversationContractsJsonContext.Default.ConversationProgress)
            ?? throw new InvalidOperationException("ProgressJson deserialized to null.");

        if (progress.ConversationId != Address.ConversationId)
            return new ConversationProgressAcceptance(
                ConversationProgressOutcome.Rejected, null, "Progress does not match this conversation.");

        // Purpose-aware admission, BEFORE the run match below. This is the structural containment
        // behind the Worker's own control rules: even a regressed or compromised Worker cannot
        // land a run, a run status, or a tool request on a control conversation — the sequence
        // allocator refuses it rather than trusting Worker discipline.
        if (checkpoint.State.Purpose == ConversationPurpose.ProjectControl)
        {
            if (RejectControlProgress(progress) is { } controlRejection)
                return new ConversationProgressAcceptance(ConversationProgressOutcome.Rejected, null, controlRejection);
        }
        else if (progress.RunId is null || progress.RunId != checkpoint.State.ActiveRunId)
        {
            return new ConversationProgressAcceptance(
                ConversationProgressOutcome.Rejected, null, "Progress does not match this conversation's active run.");
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
            progress.OccurredAtUtc);

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

        // A control fact belongs to no run, so it owes no MissionRunGrain notification. Stated
        // explicitly (rather than relying on AdvanceAsync's own null-RunId guard) so a repaired
        // transition persists the same truthful intent.
        var notifyMissionRun = checkpoint.State.Purpose != ConversationPurpose.ProjectControl;
        var stored = await PlanAppendAdvanceAsync(planned, null, notifyMissionRun, dispatchCommand, ct);
        return new ConversationProgressAcceptance(ConversationProgressOutcome.Appended, stored.Sequence, null);
    }

    /// <summary>The admissible shape of a Project-control progress fact: null run, and only the
    /// two facts the zero-tool MissionControl mission may report. Returns the rejection reason, or
    /// null when the fact is admissible. A <c>RunStatus</c> is refused outright — a control turn
    /// has no run whose status could be reported, so an interrupted turn is an <c>Error</c>.</summary>
    private static string? RejectControlProgress(ConversationProgress progress) => progress switch
    {
        { RunId: not null } => "A Project-control fact cannot name a run.",
        { Kind: not (ConversationEventKind.ParticipantMessage or ConversationEventKind.Error) } =>
            $"A Project-control fact cannot be '{progress.Kind}'; only participant messages and errors are control facts.",
        { ToolRequest: not null } or { ToolResult: not null } => "A Project-control fact cannot carry tool content.",
        { RunStatus: not null } => "A Project-control fact cannot carry a run status.",
        { Approval: not null } => "A Project-control fact cannot carry an approval.",
        { Artifact: not null } => "A Project-control fact cannot carry an artifact reference.",
        _ => null,
    };

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
        var snapshot = new ConversationSnapshot(
            Address.ConversationId, checkpoint.State.MissionRef, checkpoint.State.ActiveRunId,
            checkpoint.State.LastSequence, checkpoint.State.Status, checkpoint.State.ExpectedToolRequestId,
            checkpoint.State.UpdatedAtUtc, checkpoint.State.Purpose);

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
        await checkpoint.WriteStateAsync();

        return new ConversationCommandAcceptance(Address.ConversationId, command.RunId, queuedEvent.Sequence, ConversationRunStatus.Queued);
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

    private ConversationCommandOutcomeResult ResolveDuplicateControlMessage(
        StoredConversationEvent existing, ConversationControlMessageInput input)
    {
        var isEqual =
            existing.Event.ConversationId == Address.ConversationId &&
            existing.Event.RunId is null &&
            existing.Event.Kind == ConversationEventKind.UserMessage &&
            existing.Event.Participant == ConversationParticipant.User &&
            existing.Event.Text == input.Text;

        // The control turn appends exactly ONE event, so its own sequence is the acceptance —
        // unlike a run start, whose acceptance names the paired RunStatus(Queued) at sequence + 1.
        return isEqual
            ? new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Accepted,
                new ConversationCommandAcceptance(
                    Address.ConversationId, null, existing.Event.Sequence, checkpoint.State.Status),
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

    private static string MissionRunGrainKey(string tenantId, Guid runId) => $"{tenantId}|{runId:N}";
}
