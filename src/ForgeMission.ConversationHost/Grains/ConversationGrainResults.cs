using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Grains;

// Host-local Orleans grain-interface DTOs (Phase 43.16 Task 4). Contracts intentionally stays free
// of Orleans attributes/packages, and JsonElement must not rely on an unverified fallback
// serializer, so no Contracts record ever crosses a grain-interface boundary or lives in Orleans
// persistent state. Every type here is [GenerateSerializer]/[Id]-annotated and carries only
// primitives/enums plus source-generated JSON strings; the caller/grain (de)serializes real
// Contracts values with ConversationContractsJsonContext at the boundary.

/// <summary>Whether a pending transition's accepted command has been enqueued to the Worker.
/// <c>NotDispatched</c> only in this task — Task 5 adds the dispatched states.</summary>
public enum DispatchState
{
    NotDispatched,
}

/// <summary>
/// A recovery record, not history: one fully planned <see cref="ConversationEvent"/> (as
/// source-generated JSON) that a crash may have left un-appended or un-advanced, plus its
/// optional accepted <see cref="ConversationCommand"/> (also JSON) and dispatch state.
/// <see cref="NotifyMissionRun"/> persists whether this transition owes a
/// <c>MissionRunGrain.ApplyDurableEventAsync</c> notification once durably appended — repair
/// must reproduce the original call's intent (in particular, the interruption-report path's
/// own append must never repair into a notifying call, which would be a synchronous re-entrant
/// call back into that grain's still-executing activation).
/// </summary>
[GenerateSerializer]
public sealed record PendingConversationTransition(
    [property: Id(0)] string PlannedEventJson,
    [property: Id(1)] string? AcceptedCommandJson,
    [property: Id(2)] DispatchState DispatchState,
    [property: Id(3)] bool NotifyMissionRun);

/// <summary>Grain-interface wrapper for a <see cref="ConversationCommand"/>, serialized by the
/// caller with <see cref="ConversationContractsJsonContext"/>.</summary>
[GenerateSerializer]
public sealed record ConversationCommandInput([property: Id(0)] string CommandJson);

/// <summary>Grain-interface wrapper for a <see cref="ConversationProgress"/>, serialized by the
/// caller with <see cref="ConversationContractsJsonContext"/>.</summary>
[GenerateSerializer]
public sealed record ConversationProgressInput([property: Id(0)] string ProgressJson);

/// <summary>Grain-interface wrapper for a <see cref="ConversationSnapshot"/>; the caller
/// deserializes it with <see cref="ConversationContractsJsonContext"/>.</summary>
[GenerateSerializer]
public sealed record ConversationSnapshotResult([property: Id(0)] string SnapshotJson);

/// <summary>Grain-interface wrapper for an ordered <see cref="ConversationEvent"/> range; each
/// element is deserialized individually by the caller with <see cref="ConversationContractsJsonContext"/>.</summary>
[GenerateSerializer]
public sealed record ConversationEventBatch([property: Id(0)] string[] EventJson);

/// <summary>
/// The closed fact <c>ConversationGrain</c> reports to <c>MissionRunGrain</c> after durably
/// appending an event — carries only the fields <see cref="MissionRunGrain"/> needs to update its
/// <see cref="MissionRunExecutionBoundary"/>, never the full event (no <c>JsonElement</c> risk:
/// <see cref="ConversationEventKind"/>/<see cref="ConversationRunStatus"/> are plain enums).
/// <see cref="ConversationId"/> is carried here (an addition to the spoke's field list, flagged in
/// the implementation summary) because it is the only channel through which
/// <c>MissionRunCheckpoint.ConversationId</c> — required to reconstruct the
/// <c>ConversationAddress</c> for interruption-reporting — can ever reach the grain: the grain key
/// is fixed at <c>{TenantId}|{RunId:N}</c> and carries no conversation identity.
/// </summary>
[GenerateSerializer]
public sealed record MissionRunEventInput(
    [property: Id(0)] Guid EventId,
    [property: Id(1)] Guid RunId,
    [property: Id(2)] Guid ConversationId,
    [property: Id(3)] ConversationEventKind Kind,
    [property: Id(4)] ConversationRunStatus? RunStatus);

/// <summary>
/// <c>MissionRunGrain</c>'s dedicated report to <c>ConversationGrain</c> after it has already
/// persisted itself as <c>Interrupted</c>/<c>Terminal</c>. <see cref="EventId"/> is the stable ID
/// generated once and retried unchanged on every subsequent activation until the fact is durably
/// recorded — idempotent via the event store's own event-ID dedupe.
/// </summary>
[GenerateSerializer]
public sealed record MissionRunInterruption(
    [property: Id(0)] Guid RunId,
    [property: Id(1)] Guid EventId,
    [property: Id(2)] DateTimeOffset OccurredAtUtc);

/// <summary>Result of <c>ConversationGrain.AcceptCommandAsync</c> — names the second (
/// <c>RunStatus(Queued)</c>) planned event's sequence.</summary>
[GenerateSerializer]
public sealed record ConversationCommandAcceptance(
    [property: Id(0)] Guid ConversationId,
    [property: Id(1)] Guid RunId,
    [property: Id(2)] long AcceptedSequence,
    [property: Id(3)] ConversationRunStatus Status);

/// <summary>Distinguishes a newly appended progress fact from an already-recorded equal one from a
/// rejection (unknown/mismatched/already-completed tool result).</summary>
public enum ConversationProgressOutcome
{
    Appended,
    AlreadyRecorded,
    Rejected,
}

/// <summary>Result of <c>ConversationGrain.RecordProgressAsync</c>.</summary>
[GenerateSerializer]
public sealed record ConversationProgressAcceptance(
    [property: Id(0)] ConversationProgressOutcome Outcome,
    [property: Id(1)] long? Sequence,
    [property: Id(2)] string? RejectionReason);
