using System.Text.Json;
using ForgeMission.ConversationHost.Messaging;
using ForgeMission.ConversationHost.Persistence;
using ForgeMission.Conversations.Contracts;
using Orleans;
using Orleans.Runtime;

namespace ForgeMission.ConversationHost.Grains;

/// <summary>
/// The sole sequence allocator and event appender for one conversation. Fixed protocol for both
/// public accept operations: (1) repair any prior pending transition, (2) plan one event at
/// <c>LastSequence + 1</c> and persist it in <c>PendingTransition</c> first, (3) idempotently
/// append it, (4) advance <c>LastSequence</c>/update snapshot fields and notify
/// <c>MissionRunGrain</c> — except <see cref="RecordRunInterruptionAsync"/>, which deliberately
/// skips that notification (it is called only after MissionRunGrain has already persisted its own
/// terminal state, so calling back would be a synchronous re-entrant call into its still-executing
/// activation) — then (5) if the transition owes a mission-command send, dispatch it (a resend
/// after a broker accept is safe: the queue dedupes on <c>MessageId</c>) and only then clear
/// <c>PendingTransition</c>. A transition that owes a dispatch also owns a durable Orleans reminder
/// (<see cref="OutboxReminderName"/>) so a crash between steps is retried even if this activation
/// never restarts on its own.
/// </summary>
public sealed class ConversationGrain(
    [PersistentState("conversation-checkpoint", "conversation-checkpoint")] IPersistentState<ConversationCheckpoint> checkpoint,
    IConversationEventStore eventStore,
    IGrainFactory grainFactory,
    IConversationCommandDispatcher dispatcher)
    : Grain, IConversationGrain, IRemindable
{
    private const string OutboxReminderName = "mission-command-outbox";
    private static readonly TimeSpan OutboxReminderDueTime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan OutboxReminderPeriod = TimeSpan.FromMinutes(1);

    private ConversationAddress Address => ConversationAddress.Parse(this.GetPrimaryKeyString());

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await RepairPendingTransitionIfAnyAsync(cancellationToken);

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

    public async Task<ConversationCommandAcceptance> AcceptCommandAsync(ConversationCommandInput input)
    {
        var ct = CancellationToken.None;
        await RepairPendingTransitionIfAnyAsync(ct);

        var command = JsonSerializer.Deserialize(input.CommandJson, ConversationContractsJsonContext.Default.ConversationCommand)
            ?? throw new InvalidOperationException("CommandJson deserialized to null.");

        if (command.ConversationId != Address.ConversationId)
            throw new InvalidOperationException(
                $"Command conversation id '{command.ConversationId}' does not match this grain's address.");

        // Duplicate command acceptance resolves through the durable event-ID row (the UserMessage
        // event's EventId is the command's CommandId) — checked before allocating any new
        // sequence, so a retry never fights a fresh (wrong) sequence number against the original.
        var existingUserMessage = await eventStore.FindByEventIdAsync(Address, command.CommandId, ct);
        if (existingUserMessage is not null)
        {
            // Reconstruct the candidate at the ORIGINAL sequence/timestamp and let AppendAsync's
            // own event-ID semantic-equality invariant (now extended to the associated command
            // JSON too) decide: same command content returns cleanly, changed MissionRef/Goal/
            // Kind/Capabilities/ToolResult throws loudly. This is the single source of truth for
            // "is this really the same submission" — no separate ad-hoc check.
            var candidate = new ConversationEvent(
                command.CommandId, 1, command.ConversationId, command.RunId, existingUserMessage.Sequence,
                ConversationEventKind.UserMessage, ConversationParticipant.User, null, command.Goal, null,
                null, null, null, null, null, existingUserMessage.OccurredAtUtc);
            var commandJson = JsonSerializer.Serialize(command, ConversationContractsJsonContext.Default.ConversationCommand);
            await eventStore.AppendAsync(Address, candidate, commandJson, ct);

            var latest = await eventStore.ReadLatestForRunAsync(Address, command.RunId, ct);
            var status = latest?.RunStatus ?? ConversationRunStatus.Queued;
            var sequence = latest?.Sequence ?? existingUserMessage.Sequence;
            return new ConversationCommandAcceptance(Address.ConversationId, command.RunId, sequence, status);
        }

        if (checkpoint.State.MissionRef is { Length: > 0 } pinned && pinned != command.MissionRef)
            throw new InvalidOperationException(
                $"Conversation is pinned to mission '{pinned}'; cannot accept '{command.MissionRef}'.");

        if (checkpoint.State.ActiveRunId is not null)
            throw new InvalidOperationException(
                $"Conversation already has an active run '{checkpoint.State.ActiveRunId}'.");

        if (string.IsNullOrEmpty(checkpoint.State.MissionRef))
        {
            checkpoint.State.TenantId = Address.TenantId;
            checkpoint.State.ConversationId = Address.ConversationId;
            checkpoint.State.MissionRef = command.MissionRef;
        }

        // Folded into the same write regardless of whether this is the conversation's first-ever
        // run: ActiveStartCommandJson must always reflect the CURRENT active run's StartMission
        // command, so a later matching ToolResult can derive its ContinueAfterTool continuation
        // without a second store read.
        checkpoint.State.ActiveStartCommandJson =
            JsonSerializer.Serialize(command, ConversationContractsJsonContext.Default.ConversationCommand);
        await checkpoint.WriteStateAsync();

        var userMessage = new ConversationEvent(
            command.CommandId, 1, Address.ConversationId, command.RunId, checkpoint.State.LastSequence + 1,
            ConversationEventKind.UserMessage, ConversationParticipant.User, null, command.Goal, null,
            null, null, null, null, null, DateTimeOffset.UtcNow);
        await PlanAppendAdvanceAsync(userMessage, command, notifyMissionRun: true, dispatchCommand: null, ct);

        var queued = new ConversationEvent(
            Guid.NewGuid(), 1, Address.ConversationId, command.RunId, checkpoint.State.LastSequence + 1,
            ConversationEventKind.RunStatus, ConversationParticipant.Forge, null, null, null,
            null, null, null, null, ConversationRunStatus.Queued, DateTimeOffset.UtcNow);
        var storedQueued = await PlanAppendAdvanceAsync(queued, null, notifyMissionRun: true, dispatchCommand: command, ct);

        return new ConversationCommandAcceptance(
            Address.ConversationId, command.RunId, storedQueued.Sequence, ConversationRunStatus.Queued);
    }

    public async Task<ConversationProgressAcceptance> RecordProgressAsync(ConversationProgressInput input)
    {
        var ct = CancellationToken.None;
        await RepairPendingTransitionIfAnyAsync(ct);

        var progress = JsonSerializer.Deserialize(input.ProgressJson, ConversationContractsJsonContext.Default.ConversationProgress)
            ?? throw new InvalidOperationException("ProgressJson deserialized to null.");

        if (progress.ConversationId != Address.ConversationId || progress.RunId != checkpoint.State.ActiveRunId)
            return new ConversationProgressAcceptance(
                ConversationProgressOutcome.Rejected, null, "Progress does not match this conversation's active run.");

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
            var existingJson = JsonSerializer.Serialize(existing, ConversationContractsJsonContext.Default.ConversationEvent);
            var plannedAtExistingSequence = JsonSerializer.Serialize(
                planned with { Sequence = existing.Sequence }, ConversationContractsJsonContext.Default.ConversationEvent);

            return string.Equals(existingJson, plannedAtExistingSequence, StringComparison.Ordinal)
                ? new ConversationProgressAcceptance(ConversationProgressOutcome.AlreadyRecorded, existing.Sequence, null)
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
        var snapshot = new ConversationSnapshot(
            Address.ConversationId, checkpoint.State.MissionRef, checkpoint.State.ActiveRunId,
            checkpoint.State.LastSequence, checkpoint.State.Status, checkpoint.State.ExpectedToolRequestId,
            checkpoint.State.UpdatedAtUtc);

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
