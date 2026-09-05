using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Grains;

// Host-local Orleans grain-interface DTOs (Phase 43.16 Task 4). Contracts intentionally stays free
// of Orleans attributes/packages, and JsonElement must not rely on an unverified fallback
// serializer, so no Contracts record ever crosses a grain-interface boundary or lives in Orleans
// persistent state. Every type here is [GenerateSerializer]/[Id]-annotated and carries only
// primitives/enums plus source-generated JSON strings; the caller/grain (de)serializes real
// Contracts values with ConversationContractsJsonContext at the boundary.

/// <summary>Whether a pending transition's outbox command has been sent to the Worker.
/// <c>NotDispatched</c>: not yet sent, or a resend is still owed. <c>BrokerAccepted</c>: the
/// broker acknowledged the send — recovery must never resend after this.</summary>
public enum DispatchState
{
    NotDispatched,
    BrokerAccepted,
}

/// <summary>
/// A recovery record, not history: one fully planned <see cref="ConversationEvent"/> (as
/// source-generated JSON) that a crash may have left un-appended or un-advanced, plus its
/// optional accepted <see cref="ConversationCommand"/> (also JSON, UserMessage idempotency data
/// only — never the outbox command), an optional outbox <see cref="DispatchCommandJson"/> (the
/// StartMission or derived ContinueAfterTool command this transition owes a send for), and
/// dispatch state. <see cref="NotifyMissionRun"/> persists whether this transition owes a
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
    [property: Id(3)] bool NotifyMissionRun,
    [property: Id(4)] string? DispatchCommandJson);

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

/// <summary>Grain-interface wrapper for a follow-up submission on an existing conversation's
/// pinned mission (Task 6). The grain — never the caller — reconstructs the full
/// <see cref="ConversationCommand"/> from <see cref="ConversationCheckpoint.MissionRef"/> and
/// <see cref="ConversationCheckpoint.PinnedCapabilitiesJson"/>, so a follow-up can never select a
/// different mission or replace capabilities.</summary>
[GenerateSerializer]
public sealed record ConversationFollowupCommandInput(
    [property: Id(0)] Guid CommandId,
    [property: Id(1)] string Text);

/// <summary>Grain-interface wrapper for a client-submitted tool result (Task 6). The grain
/// constructs the matching <see cref="ConversationProgress"/> itself from its active run.</summary>
[GenerateSerializer]
public sealed record ConversationToolResultInput(
    [property: Id(0)] Guid CommandId,
    [property: Id(1)] Guid ToolRequestId,
    [property: Id(2)] string Content,
    [property: Id(3)] bool IsError);

/// <summary>Grain-interface wrapper for creating a Project's Mission Control conversation
/// (43.20 task 2). <see cref="ProjectId"/>/<see cref="ProjectGoal"/> are the create command's
/// content: an exact retry is recognised by comparing them, and a create naming a different
/// Project or goal against an already-pinned conversation is a conflict rather than a silent
/// overwrite.
///
/// <see cref="CommandId"/> is validated as non-empty but is deliberately NOT part of that content
/// comparison: the conversation id is derived from it one layer up, so two different command ids
/// address two different grains and no grain can ever see a second one. Comparing it here would
/// be a check that cannot fail.</summary>
[GenerateSerializer]
public sealed record ConversationControlCreateInput(
    [property: Id(0)] Guid CommandId,
    [property: Id(1)] Guid ProjectId,
    [property: Id(2)] string ProjectGoal);

/// <summary>Grain-interface wrapper for one Project-control turn (43.20 task 2). Deliberately has
/// NO project-goal, capability, mission, path, tool, or run member: the grain sources the goal
/// from <see cref="ConversationCheckpoint.ProjectGoal"/>, so there is no expression in
/// <c>AcceptControlMessageAsync</c> that could read one from the caller.</summary>
[GenerateSerializer]
public sealed record ConversationControlMessageInput(
    [property: Id(0)] Guid CommandId,
    [property: Id(1)] string Text);

/// <summary>Grain-interface wrapper for creating a Project's Mission container (43.21 task 1).
/// Mirrors <see cref="ConversationControlCreateInput"/>'s content-comparison idempotency, and for
/// the same reason omits any mission or capability member: a container pins neither.</summary>
[GenerateSerializer]
public sealed record ConversationProjectMissionCreateInput(
    [property: Id(0)] Guid CommandId,
    [property: Id(1)] Guid ProjectId,
    [property: Id(2)] string ProjectGoal);

/// <summary>Grain-interface wrapper for starting one child Mission Run under a Project's Mission
/// container (43.21 task 1). Unlike <see cref="ConversationFollowupCommandInput"/>, the mission
/// travels WITH the command rather than being reconstructed from the container — that is precisely
/// what lets one Project alternate between Janus and Naive. It is allow-listed before it reaches
/// here and again by the Worker's closed catalog.
///
/// There is no capability member, by the same reasoning as the wire request: a Project Mission Run
/// grants no local tool authority, and the grain declares zero capabilities for every run on this
/// route. There is likewise no member able to carry a project goal, path, provider, or run id.</summary>
[GenerateSerializer]
public sealed record ConversationProjectMissionRunInput(
    [property: Id(0)] Guid CommandId,
    [property: Id(1)] string Mission,
    [property: Id(2)] string Input);

/// <summary>Grain-interface wrapper for an ordered <see cref="ConversationEvent"/> range; each
/// element is deserialized individually by the caller with <see cref="ConversationContractsJsonContext"/>.</summary>
[GenerateSerializer]
public sealed record ConversationEventBatch([property: Id(0)] string[] EventJson);

/// <summary>Task-2 read outcome across the Orleans boundary. JSON remains Host-contract JSON so
/// Contracts stays free of Orleans annotations and the adapter owns response decoding.</summary>
[GenerateSerializer]
public sealed record ConversationProjectReadResult(
    [property: Id(0)] string? PayloadJson,
    [property: Id(1)] string? ErrorCode,
    [property: Id(2)] string? ErrorMessage);

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
/// <c>RunStatus(Queued)</c>) planned event's sequence. <see cref="RunId"/> is null for a
/// Project-control acceptance, which starts no run (43.20 task 2).</summary>
[GenerateSerializer]
public sealed record ConversationCommandAcceptance(
    [property: Id(0)] Guid ConversationId,
    [property: Id(1)] Guid? RunId,
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

/// <summary>Distinguishes an accepted start/follow-up/tool-result command from an expected,
/// non-exceptional outcome: <see cref="Conflict"/> (an active run already exists, a tool request is
/// unknown/mismatched/already completed, or a reused command/event ID carries different content),
/// <see cref="Invalid"/> (malformed/missing required fields, or a payload exceeding a fixed size
/// bound), or <see cref="NotFound"/> (the message names a conversation that does not exist). None of
/// these is ever thrown as an exception — the message handlers below classify every expected
/// outcome explicitly and return this typed result, so the HTTP adapter maps it directly to
/// 202/400/404/409 without inspecting any exception.</summary>
public enum ConversationCommandOutcome
{
    Accepted,
    Conflict,
    Invalid,
    NotFound,

    /// <summary>A Project already has a Mission Run that is queued, running, or awaiting a tool
    /// (43.21 task 1). Distinct from <see cref="Conflict"/> because it is an ordinary, expected
    /// product state a surface should explain — "one run at a time" — rather than a malformed or
    /// contradictory request. It appends no event and creates no run.</summary>
    RunAlreadyActive,
}

/// <summary>Result of <c>ConversationGrain.AcceptCommandAsync</c>,
/// <c>AcceptFollowupCommandAsync</c>, and <c>AcceptToolResultAsync</c>, and of the transport-neutral
/// message handlers that call them. <see cref="Acceptance"/> is non-null only when
/// <see cref="Outcome"/> is <see cref="ConversationCommandOutcome.Accepted"/>; <see cref="Reason"/>
/// is non-null for every other outcome.</summary>
[GenerateSerializer]
public sealed record ConversationCommandOutcomeResult(
    [property: Id(0)] ConversationCommandOutcome Outcome,
    [property: Id(1)] ConversationCommandAcceptance? Acceptance,
    [property: Id(2)] string? Reason);

/// <summary>
/// A durable recovery record for the two-fact start pair (<c>UserMessage</c> then
/// <c>RunStatus(Queued)</c>) that begins any new run — the conversation's first ever run, or any
/// later follow-up run. <see cref="QueuedEventId"/>/<see cref="QueuedOccurredAtUtc"/> are
/// preallocated and persisted in the SAME checkpoint write that begins the run, before the
/// <c>UserMessage</c> is ever appended — so a crash strictly between the two facts (after the
/// <c>UserMessage</c> is durable, before the <c>RunStatus(Queued)</c> transition has even been
/// attempted) still has a durable record of exactly what is owed and under which stable identity.
/// Cleared only once both facts have been confirmed durable (freshly appended, or found already
/// present — either way, "already present" also proves any owed dispatch was already
/// broker-accepted, since the existing pending-transition/outbox protocol never clears a pending
/// transition until that happens).
/// </summary>
[GenerateSerializer]
public sealed record PendingRunStart(
    [property: Id(0)] string StartCommandJson,
    [property: Id(1)] Guid QueuedEventId,
    [property: Id(2)] DateTimeOffset QueuedOccurredAtUtc);
