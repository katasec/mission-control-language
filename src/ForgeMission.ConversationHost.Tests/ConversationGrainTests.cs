using System.Text.Json;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.ConversationHost.Persistence;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Tests;

/// <summary>
/// Grain-level integration tests against real Azurite (Phase 43.16 Task 4). Each test uses a
/// fresh, unique tenant/conversation/run ID and starts a second Host/Silo against the same
/// Azurite endpoint to prove reactivation/replay rather than reading in-memory state.
/// </summary>
[Collection("Azurite")]
public class ConversationGrainTests(AzuriteFixture fixture)
{
    private static ConversationAddress NewAddress() => new("dev", Guid.NewGuid());

    private static string SerializeCommand(ConversationCommand command)
        => JsonSerializer.Serialize(command, ConversationContractsJsonContext.Default.ConversationCommand);

    private static string SerializeProgress(ConversationProgress progress)
        => JsonSerializer.Serialize(progress, ConversationContractsJsonContext.Default.ConversationProgress);

    private static List<ConversationEvent> DeserializeEvents(ConversationEventBatch batch)
        => [.. batch.EventJson.Select(json =>
            JsonSerializer.Deserialize(json, ConversationContractsJsonContext.Default.ConversationEvent)!)];

    private static ConversationSnapshot DeserializeSnapshot(ConversationSnapshotResult result)
        => JsonSerializer.Deserialize(result.SnapshotJson, ConversationContractsJsonContext.Default.ConversationSnapshot)!;

    private static async Task<ConversationCommandAcceptance> AcceptStartCommandAsync(
        IConversationGrain grain, ConversationAddress address, Guid runId, string goal = "goal")
    {
        var command = new ConversationCommand(
            Guid.NewGuid(), address.ConversationId, runId, ConversationCommandKind.StartMission, "Janus", goal, [], null);
        var result = await grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(command)));
        Assert.Equal(ConversationCommandOutcome.Accepted, result.Outcome);
        return result.Acceptance!;
    }

    // ── 1. Ordered events, fresh-Host replay ────────────────────────────────────

    [Fact]
    public async Task Command_CreatesOrderedEvents_FreshHostReactivatesAndReplaysInOrder()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();
        Guid commandId;
        ConversationCommandAcceptance acceptance;

        await using (var host1 = await fixture.StartHostAsync())
        {
            var grain = host1.GetConversationGrain(address);
            commandId = Guid.NewGuid();
            var command = new ConversationCommand(
                commandId, address.ConversationId, runId, ConversationCommandKind.StartMission, "Janus", "goal text", [], null);
            var result = await grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(command)));
            Assert.Equal(ConversationCommandOutcome.Accepted, result.Outcome);
            acceptance = result.Acceptance!;
        }

        Assert.Equal(2, acceptance.AcceptedSequence);
        Assert.Equal(ConversationRunStatus.Queued, acceptance.Status);

        await using var host2 = await fixture.StartHostAsync();
        var grain2 = host2.GetConversationGrain(address);
        var events = DeserializeEvents(await grain2.ReadAfterAsync(0));

        Assert.Equal(2, events.Count);
        Assert.Equal(commandId, events[0].EventId); // UserMessage's EventId is the CommandId.
        Assert.Equal(1, events[0].Sequence);
        Assert.Equal(ConversationEventKind.UserMessage, events[0].Kind);
        Assert.Equal(2, events[1].Sequence);
        Assert.Equal(ConversationEventKind.RunStatus, events[1].Kind);
        Assert.Equal(ConversationRunStatus.Queued, events[1].RunStatus);
    }

    // ── 2. Duplicate command/event IDs ───────────────────────────────────────────

    [Fact]
    public async Task RepeatingCommandId_ReturnsPriorAcceptance_NoNewEventOrSequence()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();
        var command = new ConversationCommand(
            Guid.NewGuid(), address.ConversationId, runId, ConversationCommandKind.StartMission, "Janus", "goal", [], null);
        var commandJson = SerializeCommand(command);

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);

        var first = await grain.AcceptCommandAsync(new ConversationCommandInput(commandJson));
        var second = await grain.AcceptCommandAsync(new ConversationCommandInput(commandJson));

        Assert.Equal(ConversationCommandOutcome.Accepted, first.Outcome);
        Assert.Equal(ConversationCommandOutcome.Accepted, second.Outcome);
        Assert.Equal(first.Acceptance!.AcceptedSequence, second.Acceptance!.AcceptedSequence);
        Assert.Equal(first.Acceptance.Status, second.Acceptance.Status);
        Assert.Equal(2, (await grain.ReadAfterAsync(0)).EventJson.Length);
    }

    [Fact]
    public async Task RepeatingCommandId_WithChangedMissionRef_IsTypedConflict()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();
        var original = new ConversationCommand(
            Guid.NewGuid(), address.ConversationId, runId, ConversationCommandKind.StartMission, "Janus", "goal", [], null);

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);
        await grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(original)));

        // Same CommandId, but MissionRef differs — not reflected in the UserMessage event's own
        // fields at all, so this specifically proves the associated-command-JSON comparison, not
        // just the event-text check.
        var changed = original with { MissionRef = "DifferentMission" };

        var result = await grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(changed)));

        Assert.Equal(ConversationCommandOutcome.Conflict, result.Outcome);
        Assert.Null(result.Acceptance);
        Assert.NotNull(result.Reason);
        Assert.Equal(2, (await grain.ReadAfterAsync(0)).EventJson.Length); // no new event appended
    }

    [Fact]
    public async Task RepeatingCommandId_WithChangedGoal_IsTypedConflict()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();
        var original = new ConversationCommand(
            Guid.NewGuid(), address.ConversationId, runId, ConversationCommandKind.StartMission, "Janus", "original goal", [], null);

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);
        await grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(original)));

        var changed = original with { Goal = "different goal" };

        var result = await grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(changed)));

        Assert.Equal(ConversationCommandOutcome.Conflict, result.Outcome);
        Assert.Null(result.Acceptance);
        Assert.NotNull(result.Reason);
        Assert.Equal(2, (await grain.ReadAfterAsync(0)).EventJson.Length); // no new event appended
    }

    [Fact]
    public async Task RepeatingProgressEventId_ReturnsAlreadyRecorded_NoNewEvent()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);
        await AcceptStartCommandAsync(grain, address, runId);

        var progress = new ConversationProgress(
            Guid.NewGuid(), address.ConversationId, runId, ConversationEventKind.ParticipantStarted,
            ConversationParticipant.Proposer, 1, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);
        var progressJson = SerializeProgress(progress);

        var first = await grain.RecordProgressAsync(new ConversationProgressInput(progressJson));
        var second = await grain.RecordProgressAsync(new ConversationProgressInput(progressJson));

        Assert.Equal(ConversationProgressOutcome.Appended, first.Outcome);
        Assert.Equal(ConversationProgressOutcome.AlreadyRecorded, second.Outcome);
        Assert.Equal(first.Sequence, second.Sequence);
        Assert.Equal(3, (await grain.ReadAfterAsync(0)).EventJson.Length); // UserMessage + Queued + this one, no duplicate
    }

    [Fact]
    public async Task ProgressEventId_SameIdDifferentPayload_FailsLoudlyAsRejected()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);
        await AcceptStartCommandAsync(grain, address, runId);

        var eventId = Guid.NewGuid();
        var progress1 = new ConversationProgress(
            eventId, address.ConversationId, runId, ConversationEventKind.ParticipantStarted,
            ConversationParticipant.Proposer, 1, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);
        var progress2 = progress1 with { Attempt = 2 }; // same EventId, different content

        var accepted = await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(progress1)));
        var rejected = await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(progress2)));

        Assert.Equal(ConversationProgressOutcome.Appended, accepted.Outcome);
        Assert.Equal(ConversationProgressOutcome.Rejected, rejected.Outcome);
        Assert.NotNull(rejected.RejectionReason);
    }

    // ── 3. Crash-shaped pending-transition repair ────────────────────────────────

    [Fact]
    public async Task CrashShapedPendingTransition_RepairedOnceOnActivation_NoGapOrDuplicate()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();
        var commandId = Guid.NewGuid();

        await using (var host1 = await fixture.StartHostAsync(
            store => new ThrowOnNthAppendEventStore(store, throwOnCallNumber: 2)))
        {
            var grain = host1.GetConversationGrain(address);
            var command = new ConversationCommand(
                commandId, address.ConversationId, runId, ConversationCommandKind.StartMission, "Janus", "goal", [], null);

            // The UserMessage append (call #1) succeeds and its PendingTransition is cleared; the
            // RunStatus(Queued) append (call #2) fails after its PendingTransition was already
            // durably written — a genuine crash-shaped dangling transition.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(command))));
        }

        await using var host2 = await fixture.StartHostAsync(); // fresh Silo, healthy store
        var grain2 = host2.GetConversationGrain(address);
        var events = DeserializeEvents(await grain2.ReadAfterAsync(0));

        Assert.Equal(2, events.Count);
        Assert.Equal(1, events[0].Sequence);
        Assert.Equal(ConversationEventKind.UserMessage, events[0].Kind);
        Assert.Equal(2, events[1].Sequence); // repaired exactly once — no gap, no duplicate row
        Assert.Equal(ConversationEventKind.RunStatus, events[1].Kind);
        Assert.Equal(ConversationRunStatus.Queued, events[1].RunStatus);

        var snapshot = DeserializeSnapshot(await grain2.GetSnapshotAsync());
        Assert.Equal(2, snapshot.LastSequence);
        Assert.Equal(ConversationRunStatus.Queued, snapshot.Status);
    }

    // ── 3c. PendingRunStart: the literal gap between the two start facts (Task 6) ────────
    // The test above faults inside AppendAsync itself, after a Queued-side PendingTransition was
    // already durably written. This one faults strictly BEFORE that — at the Queued event's own
    // existence check inside CompletePendingRunStartAsync, which runs after the UserMessage is
    // already fully durable/advanced but before any Queued-side PendingTransition would ever be
    // written. Only PendingRunStart's own preallocated QueuedEventId/timestamp (durably written
    // before either start fact was attempted) can recover this gap.

    [Fact]
    public async Task StartPairCrash_BetweenUserMessageAndQueuedLookup_RepairedOnceOnActivation_NoGapOrDuplicate()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var command = new ConversationCommand(
            commandId, address.ConversationId, runId, ConversationCommandKind.StartMission, "Janus", "goal", [], null);

        await using (var host1 = await fixture.StartHostAsync(
            store => new ThrowOnNthFindByEventIdEventStore(store, throwOnCallNumber: 3)))
        {
            var grain = host1.GetConversationGrain(address);

            // Call #1: AcceptCommandAsync's own duplicate-check (not found — fresh conversation).
            // Call #2: CompletePendingRunStartAsync's UserMessage-existence check (not found; the
            // UserMessage is then appended and fully advanced for real). Call #3: the Queued
            // event's OWN existence check — throws here, strictly before any Queued-side
            // PendingTransition exists.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(command))));
        }

        await using var host2 = await fixture.StartHostAsync(); // fresh Silo, healthy store
        var grain2 = host2.GetConversationGrain(address);
        var events = DeserializeEvents(await grain2.ReadAfterAsync(0));

        Assert.Equal(2, events.Count);
        Assert.Equal(1, events[0].Sequence);
        Assert.Equal(ConversationEventKind.UserMessage, events[0].Kind);
        Assert.Equal(2, events[1].Sequence); // repaired exactly once — no gap, no duplicate row
        Assert.Equal(ConversationEventKind.RunStatus, events[1].Kind);
        Assert.Equal(ConversationRunStatus.Queued, events[1].RunStatus);

        // The paired n + 1 duplicate-acceptance formula survived the repair: a resubmission of the
        // same CommandId still returns the identical, original acceptance, with no new event.
        var retry = await grain2.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(command)));
        Assert.Equal(ConversationCommandOutcome.Accepted, retry.Outcome);
        Assert.Equal(2, retry.Acceptance!.AcceptedSequence);
        Assert.Equal(ConversationRunStatus.Queued, retry.Acceptance.Status);
        Assert.Equal(2, (await grain2.ReadAfterAsync(0)).EventJson.Length);
    }

    private sealed class ThrowOnNthFindByEventIdEventStore(IConversationEventStore inner, int throwOnCallNumber) : IConversationEventStore
    {
        private int _callCount;

        public Task<StoredConversationEvent?> FindByEventIdAsync(ConversationAddress address, Guid eventId, CancellationToken ct)
        {
            if (Interlocked.Increment(ref _callCount) == throwOnCallNumber)
                throw new InvalidOperationException(
                    "Simulated crash: strictly between the UserMessage's own completion and the Queued event's existence check.");
            return inner.FindByEventIdAsync(address, eventId, ct);
        }

        public Task<ConversationEvent> AppendAsync(
            ConversationAddress address, ConversationEvent @event, string? associatedCommandJson, CancellationToken ct)
            => inner.AppendAsync(address, @event, associatedCommandJson, ct);

        public IAsyncEnumerable<ConversationEvent> ReadAfterAsync(ConversationAddress address, long sequence, CancellationToken ct)
            => inner.ReadAfterAsync(address, sequence, ct);

        public Task<ConversationEvent?> ReadLatestForRunAsync(ConversationAddress address, Guid runId, CancellationToken ct)
            => inner.ReadLatestForRunAsync(address, runId, ct);
    }

    private sealed class ThrowOnNthAppendEventStore(IConversationEventStore inner, int throwOnCallNumber) : IConversationEventStore
    {
        private int _callCount;

        public Task<StoredConversationEvent?> FindByEventIdAsync(ConversationAddress address, Guid eventId, CancellationToken ct)
            => inner.FindByEventIdAsync(address, eventId, ct);

        public Task<ConversationEvent> AppendAsync(
            ConversationAddress address, ConversationEvent @event, string? associatedCommandJson, CancellationToken ct)
        {
            if (Interlocked.Increment(ref _callCount) == throwOnCallNumber)
                throw new InvalidOperationException(
                    "Simulated crash: PendingTransition was durably persisted, but AppendAsync never completed.");
            return inner.AppendAsync(address, @event, associatedCommandJson, ct);
        }

        public IAsyncEnumerable<ConversationEvent> ReadAfterAsync(ConversationAddress address, long sequence, CancellationToken ct)
            => inner.ReadAfterAsync(address, sequence, ct);

        public Task<ConversationEvent?> ReadLatestForRunAsync(ConversationAddress address, Guid runId, CancellationToken ct)
            => inner.ReadLatestForRunAsync(address, runId, ct);
    }

    // ── 3b. Crash after the Table append, at the MissionRunGrain-notification stage ─────
    // The test above faults inside AppendAsync itself, so it never exercises the "keep
    // PendingTransition until the owed notification succeeds" rule — the event was never
    // durably appended in that scenario. This one faults strictly AFTER a real, successful
    // Table append, only at the MissionRunGrain.ApplyDurableEventAsync call, proving recovery:
    // reuses the original event's sequence (no duplicate/gap), replays the owed notification,
    // and clears PendingTransition so the next transition on this conversation succeeds cleanly.

    [Fact]
    public async Task NotificationFailureAfterTableAppend_RepairsOnFreshHost_ReplaysNotificationAndClearsPending()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        await using (var host1 = await fixture.StartHostAsync(
            decorateGrainFactory: factory => new ThrowOnNthNotifyGrainFactory(factory, throwOnCallNumber: 3)))
        {
            var grain = host1.GetConversationGrain(address);

            // Calls #1 (UserMessage) and #2 (RunStatus Queued) notify successfully.
            await AcceptStartCommandAsync(grain, address, runId);

            // Call #3: this ParticipantStarted progress's OWN Table append succeeds for real —
            // only its MissionRunGrain notification fails, after the fact.
            var progress = new ConversationProgress(
                Guid.NewGuid(), address.ConversationId, runId, ConversationEventKind.ParticipantStarted,
                ConversationParticipant.Proposer, 1, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(progress))));

            // The event is durably in the Table despite the notification failure.
            var eventsBeforeRepair = DeserializeEvents(await grain.ReadAfterAsync(0));
            Assert.Equal(3, eventsBeforeRepair.Count);
            Assert.Equal(ConversationEventKind.ParticipantStarted, eventsBeforeRepair[2].Kind);
        }

        // Fresh Host/Silo, healthy (undecorated) grain factory — activation repairs the dangling
        // notify-only transition.
        await using var host2 = await fixture.StartHostAsync();
        var grain2 = host2.GetConversationGrain(address);
        var events = DeserializeEvents(await grain2.ReadAfterAsync(0));

        // Reuses the original event ID/sequence — no duplicate, no gap.
        Assert.Equal(3, events.Count);
        Assert.Equal([1L, 2L, 3L], events.Select(e => e.Sequence));
        Assert.Equal(ConversationEventKind.ParticipantStarted, events[2].Kind);

        // The owed MissionRunGrain notification was replayed during repair: ParticipantStarted
        // maps to Running, which is not MissionRunCheckpoint's zero-value default — an
        // unambiguous signal the notify actually ran (vs. merely coinciding with a default).
        var runGrain2 = host2.GetMissionRunGrain(address.TenantId, runId);
        Assert.Equal(ConversationRunStatus.Running, await runGrain2.GetStatusAsync());

        // PendingTransition was cleared: a subsequent transition on this conversation succeeds
        // cleanly at the correct next sequence, with no interference from the repaired one.
        var followUp = new ConversationProgress(
            Guid.NewGuid(), address.ConversationId, runId, ConversationEventKind.ParticipantMessage,
            ConversationParticipant.Proposer, 1, "proposal text", null, null, null, null, null, null,
            DateTimeOffset.UtcNow);
        var followUpAcceptance = await grain2.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(followUp)));

        Assert.Equal(ConversationProgressOutcome.Appended, followUpAcceptance.Outcome);
        Assert.Equal(4, followUpAcceptance.Sequence);
    }

    // Test-only IGrainFactory decorator (see AzuriteFixture.StartHostAsync's
    // decorateGrainFactory parameter) — delegates every member to the real Orleans factory
    // except IMissionRunGrain string-key resolution, which it wraps to fail the Nth
    // ApplyDurableEventAsync call across this factory's whole lifetime. Production code (Program.cs,
    // ConversationGrain, MissionRunGrain) is never touched by this seam.
    private sealed class ThrowOnNthNotifyGrainFactory(IGrainFactory inner, int throwOnCallNumber) : IGrainFactory
    {
        private int _callCount;

        public TGrainInterface GetGrain<TGrainInterface>(string primaryKey, string? grainClassNamePrefix = null)
            where TGrainInterface : Orleans.IGrainWithStringKey
        {
            var grain = inner.GetGrain<TGrainInterface>(primaryKey, grainClassNamePrefix);
            if (grain is IMissionRunGrain missionRunGrain)
                return (TGrainInterface)(object)new ThrowOnNthCallMissionRunGrain(missionRunGrain, this);
            return grain;
        }

        internal bool TryConsumeThrow() => Interlocked.Increment(ref _callCount) == throwOnCallNumber;

        public TGrainInterface GetGrain<TGrainInterface>(Guid primaryKey, string? grainClassNamePrefix = null)
            where TGrainInterface : Orleans.IGrainWithGuidKey
            => inner.GetGrain<TGrainInterface>(primaryKey, grainClassNamePrefix);

        public TGrainInterface GetGrain<TGrainInterface>(long primaryKey, string? grainClassNamePrefix = null)
            where TGrainInterface : Orleans.IGrainWithIntegerKey
            => inner.GetGrain<TGrainInterface>(primaryKey, grainClassNamePrefix);

        public TGrainInterface GetGrain<TGrainInterface>(Guid primaryKey, string keyExtension, string? grainClassNamePrefix = null)
            where TGrainInterface : Orleans.IGrainWithGuidCompoundKey
            => inner.GetGrain<TGrainInterface>(primaryKey, keyExtension, grainClassNamePrefix);

        public TGrainInterface GetGrain<TGrainInterface>(long primaryKey, string keyExtension, string? grainClassNamePrefix = null)
            where TGrainInterface : Orleans.IGrainWithIntegerCompoundKey
            => inner.GetGrain<TGrainInterface>(primaryKey, keyExtension, grainClassNamePrefix);

        public TGrainObserverInterface CreateObjectReference<TGrainObserverInterface>(Orleans.IGrainObserver obj)
            where TGrainObserverInterface : Orleans.IGrainObserver
            => inner.CreateObjectReference<TGrainObserverInterface>(obj);

        public void DeleteObjectReference<TGrainObserverInterface>(Orleans.IGrainObserver obj)
            where TGrainObserverInterface : Orleans.IGrainObserver
            => inner.DeleteObjectReference<TGrainObserverInterface>(obj);

        public Orleans.IGrain GetGrain(Type grainInterfaceType, Guid grainPrimaryKey) => inner.GetGrain(grainInterfaceType, grainPrimaryKey);
        public Orleans.IGrain GetGrain(Type grainInterfaceType, long grainPrimaryKey) => inner.GetGrain(grainInterfaceType, grainPrimaryKey);
        public Orleans.IGrain GetGrain(Type grainInterfaceType, string grainPrimaryKey) => inner.GetGrain(grainInterfaceType, grainPrimaryKey);
        public Orleans.IGrain GetGrain(Type grainInterfaceType, Guid grainPrimaryKey, string keyExtension) => inner.GetGrain(grainInterfaceType, grainPrimaryKey, keyExtension);
        public Orleans.IGrain GetGrain(Type grainInterfaceType, long grainPrimaryKey, string keyExtension) => inner.GetGrain(grainInterfaceType, grainPrimaryKey, keyExtension);

        public TGrainInterface GetGrain<TGrainInterface>(Orleans.Runtime.GrainId grainId)
            where TGrainInterface : Orleans.Runtime.IAddressable
            => inner.GetGrain<TGrainInterface>(grainId);

        public Orleans.Runtime.IAddressable GetGrain(Orleans.Runtime.GrainId grainId) => inner.GetGrain(grainId);

        public Orleans.Runtime.IAddressable GetGrain(Orleans.Runtime.GrainId grainId, Orleans.Runtime.GrainInterfaceType interfaceType)
            => inner.GetGrain(grainId, interfaceType);

        public Orleans.Runtime.IAddressable GetGrain(Type interfaceType, Orleans.Runtime.IdSpan grainKey, string grainClassNamePrefix)
            => inner.GetGrain(interfaceType, grainKey, grainClassNamePrefix);

        public Orleans.Runtime.IAddressable GetGrain(Type interfaceType, Orleans.Runtime.IdSpan grainKey)
            => inner.GetGrain(interfaceType, grainKey);
    }

    private sealed class ThrowOnNthCallMissionRunGrain(IMissionRunGrain inner, ThrowOnNthNotifyGrainFactory factory) : IMissionRunGrain
    {
        public Task ApplyDurableEventAsync(MissionRunEventInput @event)
        {
            if (factory.TryConsumeThrow())
                throw new InvalidOperationException(
                    "Simulated crash: the event Table append already succeeded, but the MissionRunGrain notification never completed.");
            return inner.ApplyDurableEventAsync(@event);
        }

        public Task<ConversationRunStatus> GetStatusAsync() => inner.GetStatusAsync();
    }

    // ── 4. MissionRunGrain activation repair ─────────────────────────────────────

    [Fact]
    public async Task ExecutingProvider_ReactivatesAsInterrupted_AndReportsThroughConversationGrain_NoProviderOrQueueCall()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        await using (var host1 = await fixture.StartHostAsync())
        {
            var conversationGrain = host1.GetConversationGrain(address);
            await AcceptStartCommandAsync(conversationGrain, address, runId);

            var runGrain = host1.GetMissionRunGrain(address.TenantId, runId);
            await runGrain.ApplyDurableEventAsync(new MissionRunEventInput(
                Guid.NewGuid(), runId, address.ConversationId, ConversationEventKind.ParticipantStarted, null));

            Assert.Equal(ConversationRunStatus.Running, await runGrain.GetStatusAsync());
        }

        // Fresh Host/Silo. Task 4 wires no provider/queue at all, so "no provider/queue call" is
        // structurally guaranteed — the only observation available is the durable status/transcript.
        await using var host2 = await fixture.StartHostAsync();
        var runGrain2 = host2.GetMissionRunGrain(address.TenantId, runId);

        Assert.Equal(ConversationRunStatus.Interrupted, await runGrain2.GetStatusAsync());

        var conversationGrain2 = host2.GetConversationGrain(address);
        var events = DeserializeEvents(await conversationGrain2.ReadAfterAsync(0));
        Assert.Contains(events, e => e.Kind == ConversationEventKind.RunStatus && e.RunStatus == ConversationRunStatus.Interrupted);
    }

    [Fact]
    public async Task WaitingForTool_RemainsWaiting_AfterReactivation()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        await using (var host1 = await fixture.StartHostAsync())
        {
            var runGrain = host1.GetMissionRunGrain(address.TenantId, runId);
            await runGrain.ApplyDurableEventAsync(new MissionRunEventInput(
                Guid.NewGuid(), runId, address.ConversationId, ConversationEventKind.ToolRequested, null));

            Assert.Equal(ConversationRunStatus.WaitingForTool, await runGrain.GetStatusAsync());
        }

        await using var host2 = await fixture.StartHostAsync();
        var runGrain2 = host2.GetMissionRunGrain(address.TenantId, runId);
        Assert.Equal(ConversationRunStatus.WaitingForTool, await runGrain2.GetStatusAsync());
    }

    // ── ConversationCheckpoint snapshot reflects WaitingForTool (review fix) ────

    [Fact]
    public async Task ToolRequestedProgress_UpdatesSnapshotStatusToWaitingForTool_AndSurvivesReactivation()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        using var argumentsDoc = JsonDocument.Parse("""{"path":"mission/notes.md"}""");
        var toolRequest = new ConversationToolRequest(Guid.NewGuid(), "read_file", argumentsDoc.RootElement);

        await using (var host1 = await fixture.StartHostAsync())
        {
            var grain = host1.GetConversationGrain(address);
            await AcceptStartCommandAsync(grain, address, runId);

            var progress = new ConversationProgress(
                Guid.NewGuid(), address.ConversationId, runId, ConversationEventKind.ToolRequested,
                ConversationParticipant.Implementer, 1, null, null, null, toolRequest, null, null, null,
                DateTimeOffset.UtcNow);
            await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(progress)));

            var snapshot = DeserializeSnapshot(await grain.GetSnapshotAsync());
            Assert.Equal(ConversationRunStatus.WaitingForTool, snapshot.Status);
            Assert.Equal(toolRequest.RequestId, snapshot.ExpectedToolRequestId);
        }

        await using var host2 = await fixture.StartHostAsync();
        var grain2 = host2.GetConversationGrain(address);
        var snapshot2 = DeserializeSnapshot(await grain2.GetSnapshotAsync());

        Assert.Equal(ConversationRunStatus.WaitingForTool, snapshot2.Status);
        Assert.Equal(toolRequest.RequestId, snapshot2.ExpectedToolRequestId);
    }

    // ── 6. Tool-shaped JsonElement across a real grain call ──────────────────────

    [Fact]
    public async Task ToolShapedJsonElementPayload_CrossesGrainCall_SurvivesReactivation()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        using var argumentsDoc = JsonDocument.Parse("""{"path":"mission/notes.md","recursive":true}""");
        var toolRequest = new ConversationToolRequest(Guid.NewGuid(), "read_file", argumentsDoc.RootElement);

        await using (var host1 = await fixture.StartHostAsync())
        {
            var grain = host1.GetConversationGrain(address);
            await AcceptStartCommandAsync(grain, address, runId);

            var progress = new ConversationProgress(
                Guid.NewGuid(), address.ConversationId, runId, ConversationEventKind.ToolRequested,
                ConversationParticipant.Implementer, 1, null, null, null, toolRequest, null, null, null,
                DateTimeOffset.UtcNow);

            var acceptance = await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(progress)));
            Assert.Equal(ConversationProgressOutcome.Appended, acceptance.Outcome);
        }

        await using var host2 = await fixture.StartHostAsync();
        var grain2 = host2.GetConversationGrain(address);
        var events = DeserializeEvents(await grain2.ReadAfterAsync(0));

        var toolRequested = Assert.Single(events, e => e.Kind == ConversationEventKind.ToolRequested);
        Assert.Equal("mission/notes.md", toolRequested.ToolRequest!.Arguments.GetProperty("path").GetString());
        Assert.True(toolRequested.ToolRequest.Arguments.GetProperty("recursive").GetBoolean());
    }

    // ── 7. Follow-up commands (Task 6) ───────────────────────────────────────────

    [Fact]
    public async Task Followup_UsesPinnedCapabilities_NotClientSupplied()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        using var schemaDoc = JsonDocument.Parse("""{"type":"object"}""");
        var capabilities = new[] { new ConversationCapabilityDeclaration("read_file", "Reads a file", schemaDoc.RootElement) };

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);

        var startCommand = new ConversationCommand(
            Guid.NewGuid(), address.ConversationId, runId, ConversationCommandKind.StartMission, "Janus", "goal", capabilities, null);
        var startResult = await grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(startCommand)));
        Assert.Equal(ConversationCommandOutcome.Accepted, startResult.Outcome);

        // Terminate the first run so a follow-up is allowed.
        var completed = new ConversationProgress(
            Guid.NewGuid(), address.ConversationId, runId, ConversationEventKind.RunStatus,
            ConversationParticipant.Forge, null, null, null, null, null, null, null,
            ConversationRunStatus.Completed, DateTimeOffset.UtcNow);
        var completeAcceptance = await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(completed)));
        Assert.Equal(ConversationProgressOutcome.Appended, completeAcceptance.Outcome);

        var followupResult = await grain.AcceptFollowupCommandAsync(
            new ConversationFollowupCommandInput(Guid.NewGuid(), "follow-up text"));
        Assert.Equal(ConversationCommandOutcome.Accepted, followupResult.Outcome);
        Assert.NotEqual(runId, followupResult.Acceptance!.RunId); // a genuinely new run

        var followupDispatch = Assert.Single(
            host.Dispatcher.Sent, s => s.Command.Kind == ConversationCommandKind.StartMission && s.Command.Goal == "follow-up text");

        Assert.Equal(capabilities.Length, followupDispatch.Command.Capabilities.Length);
        Assert.Equal(capabilities[0].Name, followupDispatch.Command.Capabilities[0].Name);
        Assert.Equal(capabilities[0].Description, followupDispatch.Command.Capabilities[0].Description);
    }

    [Fact]
    public async Task AcceptCommand_ReusingEventIdFromDifferentKind_IsTypedConflict()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);
        await AcceptStartCommandAsync(grain, address, runId);

        var collidingId = Guid.NewGuid();
        var participantStarted = new ConversationProgress(
            collidingId, address.ConversationId, runId, ConversationEventKind.ParticipantStarted,
            ConversationParticipant.Proposer, 1, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);
        var progressAcceptance = await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(participantStarted)));
        Assert.Equal(ConversationProgressOutcome.Appended, progressAcceptance.Outcome);

        var reusedIdCommand = new ConversationCommand(
            collidingId, address.ConversationId, Guid.NewGuid(), ConversationCommandKind.StartMission, "Janus", "goal", [], null);
        var result = await grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(reusedIdCommand)));

        Assert.Equal(ConversationCommandOutcome.Conflict, result.Outcome);
        Assert.Null(result.Acceptance);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public async Task AcceptFollowup_ReusingEventIdFromDifferentKind_IsTypedConflict()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);
        await AcceptStartCommandAsync(grain, address, runId);

        var collidingId = Guid.NewGuid();
        var participantStarted = new ConversationProgress(
            collidingId, address.ConversationId, runId, ConversationEventKind.ParticipantStarted,
            ConversationParticipant.Proposer, 1, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);
        await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(participantStarted)));

        var completed = new ConversationProgress(
            Guid.NewGuid(), address.ConversationId, runId, ConversationEventKind.RunStatus,
            ConversationParticipant.Forge, null, null, null, null, null, null, null,
            ConversationRunStatus.Completed, DateTimeOffset.UtcNow);
        await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(completed)));

        var result = await grain.AcceptFollowupCommandAsync(new ConversationFollowupCommandInput(collidingId, "some text"));

        Assert.Equal(ConversationCommandOutcome.Conflict, result.Outcome);
        Assert.Null(result.Acceptance);
        Assert.NotNull(result.Reason);
    }

    // ── 8. Tool results (Task 6) ─────────────────────────────────────────────────

    [Fact]
    public async Task DuplicateToolResult_ReturnsOriginalAcceptance_NoNewEventOrDispatch()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);
        await AcceptStartCommandAsync(grain, address, runId);

        var toolRequestId = Guid.NewGuid();
        using var argsDoc = JsonDocument.Parse("""{"path":"mission/notes.md"}""");
        var toolRequestedProgress = new ConversationProgress(
            Guid.NewGuid(), address.ConversationId, runId, ConversationEventKind.ToolRequested,
            ConversationParticipant.Implementer, 1, null, null, null,
            new ConversationToolRequest(toolRequestId, "read_file", argsDoc.RootElement), null, null, null, DateTimeOffset.UtcNow);
        await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(toolRequestedProgress)));

        var input = new ConversationToolResultInput(Guid.NewGuid(), toolRequestId, "file contents", false);

        var first = await grain.AcceptToolResultAsync(input);
        var second = await grain.AcceptToolResultAsync(input);

        Assert.Equal(ConversationCommandOutcome.Accepted, first.Outcome);
        Assert.Equal(ConversationCommandOutcome.Accepted, second.Outcome);
        Assert.Equal(first.Acceptance!.AcceptedSequence, second.Acceptance!.AcceptedSequence);
        Assert.Equal(ConversationRunStatus.WaitingForTool, first.Acceptance.Status);
        Assert.Equal(ConversationRunStatus.WaitingForTool, second.Acceptance.Status);

        var events = DeserializeEvents(await grain.ReadAfterAsync(0));
        Assert.Single(events, e => e.Kind == ConversationEventKind.ToolResult);
        Assert.Single(host.Dispatcher.Sent, s => s.Command.Kind == ConversationCommandKind.ContinueAfterTool);
    }

    // ── 9. Fixed payload bounds (Task 6 correction) ──────────────────────────────

    [Fact]
    public async Task AcceptCommand_OversizedGoal_ReturnsInvalid_NoStateAdvance()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);

        var oversizedGoal = new string('x', 40_000); // pushes serialized ConversationCommand JSON past 32 KiB
        var command = new ConversationCommand(
            Guid.NewGuid(), address.ConversationId, runId, ConversationCommandKind.StartMission, "Janus", oversizedGoal, [], null);
        var result = await grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(command)));

        Assert.Equal(ConversationCommandOutcome.Invalid, result.Outcome);
        Assert.Null(result.Acceptance);
        Assert.NotNull(result.Reason);
        Assert.Empty((await grain.ReadAfterAsync(0)).EventJson);
    }

    [Fact]
    public async Task AcceptToolResult_OversizedContent_ReturnsInvalid_NoStateAdvance()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);
        await AcceptStartCommandAsync(grain, address, runId);

        var toolRequestId = Guid.NewGuid();
        using var argsDoc = JsonDocument.Parse("""{"path":"mission/notes.md"}""");
        var toolRequested = new ConversationProgress(
            Guid.NewGuid(), address.ConversationId, runId, ConversationEventKind.ToolRequested,
            ConversationParticipant.Implementer, 1, null, null, null,
            new ConversationToolRequest(toolRequestId, "read_file", argsDoc.RootElement), null, null, null, DateTimeOffset.UtcNow);
        await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(toolRequested)));

        var eventsBefore = await grain.ReadAfterAsync(0);
        var oversizedContent = new string('y', 60_000); // pushes the derived ConversationProgress JSON past 48 KiB
        var result = await grain.AcceptToolResultAsync(new ConversationToolResultInput(Guid.NewGuid(), toolRequestId, oversizedContent, false));

        Assert.Equal(ConversationCommandOutcome.Invalid, result.Outcome);
        Assert.Null(result.Acceptance);
        Assert.NotNull(result.Reason);
        var eventsAfter = await grain.ReadAfterAsync(0);
        Assert.Equal(eventsBefore.EventJson.Length, eventsAfter.EventJson.Length);
    }

    [Fact]
    public async Task AcceptToolResult_ReusingEventIdFromDifferentKind_IsTypedConflict()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);
        await AcceptStartCommandAsync(grain, address, runId);

        var collidingId = Guid.NewGuid();
        var participantStarted = new ConversationProgress(
            collidingId, address.ConversationId, runId, ConversationEventKind.ParticipantStarted,
            ConversationParticipant.Proposer, 1, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);
        await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(participantStarted)));

        var result = await grain.AcceptToolResultAsync(new ConversationToolResultInput(collidingId, Guid.NewGuid(), "content", false));

        Assert.Equal(ConversationCommandOutcome.Conflict, result.Outcome);
        Assert.Null(result.Acceptance);
        Assert.NotNull(result.Reason);
    }
}
