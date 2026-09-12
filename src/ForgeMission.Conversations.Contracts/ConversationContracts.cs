using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForgeMission.Conversations.Contracts;

// Durable conversation wire contracts (Phase 43.16 Task 2). Data and project-seam only: no
// endpoint, grain, queue, or MCL-execution behaviour lives here. Shared by ConversationHost (the
// server/Silo) and, from Task 7, ForgeMission.ClientRuntime — so every type here must stay
// AOT-safe and free of any Host/Orleans/Azure/provider dependency. See
// docs/design/durable-conversations.md and docs/phases/phase-43.16-janus-desktop-local-poc.md.

/// <summary>Who produced a <see cref="ConversationEvent"/> or <see cref="ConversationProgress"/> fact.
/// <see cref="MissionControl"/> is appended last so no existing member's ordinal moves.</summary>
public enum ConversationParticipant
{
    [JsonStringEnumMemberName("user")]           User,
    [JsonStringEnumMemberName("proposer")]       Proposer,
    [JsonStringEnumMemberName("approver")]       Approver,
    [JsonStringEnumMemberName("implementer")]    Implementer,
    [JsonStringEnumMemberName("forge")]          Forge,
    // Retained solely to deserialize historic Project Control transcripts. It is not emitted by
    // an active writer.
    [JsonStringEnumMemberName("missionControl")] MissionControl,
    // The one-expert Naive mission's own voice (43.21 task 1). Appended last so no existing
    // member's ordinal moves, and deliberately not "forge": a mission's output is labelled as
    // that mission, never as the product.
    [JsonStringEnumMemberName("naive")]          Naive,
}

/// <summary>
/// What a conversation is for. <see cref="MissionRun"/> MUST stay ordinal 0: every
/// <c>ConversationCheckpoint</c> persisted before this field existed deserializes it as
/// <c>default</c>, and that default has to keep meaning "the existing Janus run conversation".
/// Reversing these two would reinterpret historical records. <see cref="ProjectControl"/> stays
/// readable but is permanently read-only.
/// </summary>
public enum ConversationPurpose
{
    [JsonStringEnumMemberName("missionRun")]     MissionRun,
    [JsonStringEnumMemberName("projectControl")] ProjectControl,

    /// <summary>A Project's Mission container (43.21 task 1): it orders and replays that
    /// Project's child mission runs and executes nothing itself. Appended last for the same
    /// ordinal-stability reason as the members above. Unlike <see cref="MissionRun"/> it pins no
    /// mission and no capabilities — each child run carries its own — so its snapshot reports a
    /// null mission reference rather than an empty string standing in for one.</summary>
    [JsonStringEnumMemberName("projectMission")]  ProjectMission,
    [JsonStringEnumMemberName("missionConversation")] MissionConversation,
    [JsonStringEnumMemberName("evaluation")] Evaluation,
}

/// <summary>The semantic kind of one durable conversation fact. Each kind has exactly one
/// relevant payload field on <see cref="ConversationEvent"/>/<see cref="ConversationProgress"/> —
/// there is no generic JSON payload.</summary>
public enum ConversationEventKind
{
    [JsonStringEnumMemberName("userMessage")]        UserMessage,
    [JsonStringEnumMemberName("participantStarted")] ParticipantStarted,
    [JsonStringEnumMemberName("participantMessage")] ParticipantMessage,
    [JsonStringEnumMemberName("approval")]           Approval,
    [JsonStringEnumMemberName("toolRequested")]      ToolRequested,
    [JsonStringEnumMemberName("toolResult")]         ToolResult,
    [JsonStringEnumMemberName("runStatus")]          RunStatus,
    [JsonStringEnumMemberName("artifact")]           Artifact,
    [JsonStringEnumMemberName("error")]              Error,
    [JsonStringEnumMemberName("missionHandsRequested")] MissionHandsRequested,
    [JsonStringEnumMemberName("missionHandsAwaiting")] MissionHandsAwaiting,
    [JsonStringEnumMemberName("missionHandsResult")] MissionHandsResult,
    [JsonStringEnumMemberName("missionHandsAwaitingToolConfirmation")] MissionHandsAwaitingToolConfirmation,
    [JsonStringEnumMemberName("missionHandsCancelled")] MissionHandsCancelled,
    [JsonStringEnumMemberName("missionHandsInFlight")] MissionHandsInFlight,
    [JsonStringEnumMemberName("missionHandsInterrupted")] MissionHandsInterrupted,
}

/// <summary>Terminal/in-flight status of one Janus run.</summary>
public enum ConversationRunStatus
{
    [JsonStringEnumMemberName("queued")]         Queued,
    [JsonStringEnumMemberName("running")]        Running,
    [JsonStringEnumMemberName("waitingForTool")] WaitingForTool,
    [JsonStringEnumMemberName("completed")]      Completed,
    [JsonStringEnumMemberName("rejected")]       Rejected,
    // Set when a run is found executing without a completed safe boundary after a restart — an
    // uncertain in-flight provider call is never silently replayed. See durable-conversations.md.
    [JsonStringEnumMemberName("interrupted")]    Interrupted,
    [JsonStringEnumMemberName("failed")]         Failed,
}

/// <summary>Outcome of an Approver decision.</summary>
public enum ConversationApprovalOutcome
{
    [JsonStringEnumMemberName("approved")]          Approved,
    [JsonStringEnumMemberName("revisionRequested")] RevisionRequested,
    [JsonStringEnumMemberName("notApproved")]       NotApproved,
}

/// <summary>The two commands the Conversation service can send the Worker.</summary>
public enum ConversationCommandKind
{
    [JsonStringEnumMemberName("startMission")]       StartMission,
    [JsonStringEnumMemberName("continueAfterTool")]  ContinueAfterTool,
}

/// <summary>Payload for <see cref="ConversationEventKind.Approval"/>.</summary>
public sealed record ConversationApproval(
    ConversationApprovalOutcome Outcome,
    string? Feedback);

/// <summary>Payload for <see cref="ConversationEventKind.ToolRequested"/>. <see cref="Arguments"/>
/// may contain a mission-relative path but never a desktop workspace root or other local-machine
/// path.</summary>
public sealed record ConversationToolRequest(
    Guid RequestId,
    string ToolName,
    JsonElement Arguments);

/// <summary>Payload for <see cref="ConversationEventKind.ToolResult"/>.</summary>
public sealed record ConversationToolResult(
    Guid RequestId,
    string Content,
    bool IsError);

/// <summary>Payload for <see cref="ConversationEventKind.Artifact"/> — a reference to Blob-stored
/// content, never the raw bytes.</summary>
public sealed record ConversationArtifactReference(
    string ArtifactId,
    string ContentType,
    string? FileName);

/// <summary>A capability the submitting client makes available to the run.</summary>
public sealed record ConversationCapabilityDeclaration(
    string Name,
    string Description,
    JsonElement InputSchema);

/// <summary>
/// One durable, canonical fact in a conversation's event log. <see cref="Version"/> is <c>1</c> for
/// this implementation. <see cref="Sequence"/> is assigned only by <c>ConversationGrain</c> — a
/// Worker never supplies it. <see cref="RunId"/> is null only for a future conversation-level fact,
/// never for a Janus v1 run event. Exactly one of <see cref="Text"/>, <see cref="Reason"/>,
/// <see cref="Approval"/>, <see cref="ToolRequest"/>, <see cref="ToolResult"/>,
/// <see cref="Artifact"/>, or <see cref="RunStatus"/> is populated, matching <see cref="Kind"/>:
/// <see cref="ConversationEventKind.UserMessage"/>/<see cref="ConversationEventKind.ParticipantMessage"/>/
/// <see cref="ConversationEventKind.Error"/> use <see cref="Text"/>/<see cref="Reason"/>;
/// <see cref="ConversationEventKind.Approval"/> uses <see cref="Approval"/>; the tool kinds use their
/// matching tool record; <see cref="ConversationEventKind.RunStatus"/> uses <see cref="RunStatus"/>;
/// <see cref="ConversationEventKind.Artifact"/> uses <see cref="Artifact"/>;
/// <see cref="ConversationEventKind.ParticipantStarted"/> has no additional payload.
/// </summary>
public sealed record ConversationEvent(
    Guid EventId,
    int Version,
    Guid ConversationId,
    Guid? RunId,
    long Sequence,
    ConversationEventKind Kind,
    ConversationParticipant Participant,
    int? Attempt,
    string? Text,
    string? Reason,
    ConversationApproval? Approval,
    ConversationToolRequest? ToolRequest,
    ConversationToolResult? ToolResult,
    ConversationArtifactReference? Artifact,
    ConversationRunStatus? RunStatus,
    DateTimeOffset OccurredAtUtc,
    MissionToolRequest? MissionHandsRequest = null,
    MissionToolOutcome? MissionHandsOutcome = null,
    /// <summary>The immutable package expert name this fact came from, copied unchanged from the
    /// Worker's own <c>PipelineTraceEvent.ExpertName</c> (Phase 48). Host stores it and derives
    /// nothing from it; a surface may prefer it over <see cref="Participant"/> for display only.
    /// Appended last so an older stored event's positional shape is unchanged.</summary>
    string? ActorName = null);

/// <summary>Compact operational checkpoint for a conversation — the projection returned by
/// <c>GET /conversations/{conversationId}</c>. The event log, not this snapshot, is canonical.</summary>
/// <param name="Purpose">Which kind of conversation this is. <see cref="Status"/>,
/// <see cref="ActiveRunId"/> and <see cref="ExpectedToolRequestId"/> describe a run lifecycle and
/// are not meaningful for historic <see cref="ConversationPurpose.ProjectControl"/> records,
/// which remain readable but cannot be mutated.</param>
public sealed record ConversationSnapshot(
    Guid ConversationId,
    /// <summary>The pinned mission for a <see cref="ConversationPurpose.MissionRun"/> conversation.
    /// Null for a Project Mission container, which pins none (43.21 task 1) — a null says "this
    /// conversation has no mission" where an empty string would be an ambiguous sentinel.</summary>
    string? MissionRef,
    Guid? ActiveRunId,
    long LastSequence,
    ConversationRunStatus Status,
    Guid? ExpectedToolRequestId,
    DateTimeOffset UpdatedAtUtc,
    ConversationPurpose Purpose = ConversationPurpose.MissionRun,
    /// <summary>The Project a historic <see cref="ConversationPurpose.ProjectControl"/> or active
    /// <see cref="ConversationPurpose.ProjectMission"/> conversation belongs to; null otherwise.
    /// Appended last so an older snapshot's positional shape is unchanged. For a Project Mission
    /// container this is what makes existence checkable at all: it pins no mission, so a non-null
    /// Project ID paired with that purpose IS its existence invariant (43.21 task 1).</summary>
    Guid? ProjectId = null,
    DurableMissionLaunch? PinnedLaunch = null,
    Guid? EvaluationResultId = null,
    Guid? EvaluationTurnId = null,
    Guid? EvaluationTurnAttemptId = null,
    string? EvaluationSummary = null,
    string? EvaluationReason = null,
    /// <summary>The durable display title of a Mission Conversation (Phase 48): <c>New chat</c>
    /// until its first turn, then a bounded normalized prefix of that first user message. Host
    /// owns it, never calls a model to make it, and never changes it again. Appended last.</summary>
    string? Title = null);

/// <summary>
/// Command queue body sent from the Conversation service to the Worker over the
/// <c>mission-command</c> queue. <see cref="CommandId"/> is generated by the submitting client and
/// is the queue's <c>MessageId</c>; <see cref="ConversationId"/> is the queue's <c>SessionId</c>.
/// Carries mission, goal/continuation, and capability declarations so the Worker needs no
/// conversation-store read. Contains neither credentials nor local workspace paths.
/// </summary>
/// <param name="RunId">Non-null for every active mission-run command. Null is retained only for
/// historic Project Control queue bodies, which remain deserializable but resolve as unsupported.</param>
/// <param name="ProjectGoal">The pinned Project goal for an active Project Mission child run.
/// Historic Project Control bodies may carry it for deserialization; active generic MissionRun
/// commands carrying it are rejected.</param>
public sealed record ConversationCommand(
    Guid CommandId,
    Guid ConversationId,
    Guid? RunId,
    ConversationCommandKind Kind,
    string MissionRef,
    string Goal,
    ConversationCapabilityDeclaration[] Capabilities,
    ConversationToolResult? ToolResult,
    string? ProjectGoal = null,
    DurableMissionLaunch? Launch = null,
    string? OpaqueContinuation = null,
    string? ProviderToolCallId = null,
    Guid? TurnId = null);

/// <summary>
/// Progress queue body sent from the Worker to the Conversation service over the
/// <c>conversation-progress</c> queue. <see cref="EventId"/> is generated once by the Worker and is
/// the queue's <c>MessageId</c>; <see cref="ConversationId"/> is the queue's <c>SessionId</c>.
/// Deliberately has no sequence — the Conversation service assigns it when converting this fact
/// into the canonical <see cref="ConversationEvent"/> through the grain.
/// </summary>
/// <param name="RunId">Non-null for active mission facts. Null remains parseable only for
/// historic Project Control facts, which Host refuses to append after retirement.</param>
public sealed record ConversationProgress(
    Guid EventId,
    Guid ConversationId,
    Guid? RunId,
    ConversationEventKind Kind,
    ConversationParticipant Participant,
    int? Attempt,
    string? Text,
    string? Reason,
    ConversationApproval? Approval,
    ConversationToolRequest? ToolRequest,
    ConversationToolResult? ToolResult,
    ConversationArtifactReference? Artifact,
    ConversationRunStatus? RunStatus,
    DateTimeOffset OccurredAtUtc,
    MissionToolRequest? MissionHandsRequest = null,
    /// <summary>The Worker's own immutable package expert name for a started/completed step
    /// (Phase 48), copied verbatim from <c>PipelineTraceEvent.ExpertName</c>. It maps no mission
    /// name, chooses no persona, and grants nothing. Appended last.</summary>
    string? ActorName = null);

// --- HTTP request/response contract -------------------------------------------------------
// Tenant/user identity is authenticated at Tier 1 (a later ForgeUI/ForgeAPI adapter) and is
// therefore deliberately absent from every request below — it is never a client-supplied field.

/// <summary><c>POST /conversations</c> request.</summary>
public sealed record StartConversationRequest(
    Guid CommandId,
    string MissionRef,
    string Goal,
    ConversationCapabilityDeclaration[] Capabilities);

/// <summary><c>POST /conversations</c> response (<c>201 Created</c>).</summary>
public sealed record StartConversationResponse(
    Guid ConversationId,
    Guid RunId,
    long AcceptedSequence,
    ConversationRunStatus Status);

/// <summary><c>POST /conversations/{conversationId}/commands</c> request. Cannot select a
/// different mission or replace capabilities — it is a follow-up on the conversation's pinned
/// <c>MissionRef</c>. <see cref="ConversationId"/> is part of the message itself (not only an HTTP
/// route value) so a future direct/gRPC/broker adapter can invoke this operation without
/// reconstructing meaning from a URL.</summary>
public sealed record SubmitConversationCommandRequest(
    Guid ConversationId,
    Guid CommandId,
    string Text);

/// <summary><c>POST /conversations/{conversationId}/commands</c> response (<c>202 Accepted</c>).</summary>
public sealed record SubmitConversationCommandResponse(
    Guid ConversationId,
    Guid RunId,
    long AcceptedSequence,
    ConversationRunStatus Status);

/// <summary><c>POST /conversations/{conversationId}/tool-results</c> request. <see cref="ConversationId"/>
/// is part of the message itself, mirroring <see cref="SubmitConversationCommandRequest"/>.</summary>
public sealed record SubmitToolResultRequest(
    Guid ConversationId,
    Guid CommandId,
    Guid ToolRequestId,
    string Content,
    bool IsError);

/// <summary><c>POST /conversations/{conversationId}/tool-results</c> response (<c>202 Accepted</c>).</summary>
public sealed record SubmitToolResultResponse(
    Guid ConversationId,
    Guid RunId,
    long AcceptedSequence,
    ConversationRunStatus Status);

/// <summary><c>GET /conversations/{conversationId}</c> request.</summary>
public sealed record GetConversationRequest(Guid ConversationId);

/// <summary><c>GET /conversations/{conversationId}</c> response (<c>200 OK</c>).</summary>
public sealed record GetConversationResponse(ConversationSnapshot Snapshot);

/// <summary><c>GET /conversations/{conversationId}/events?after={sequence}</c> request. Projected
/// as an SSE stream by the HTTP adapter; the message itself carries no transport framing.</summary>
public sealed record ReadConversationEventsRequest(Guid ConversationId, long After);

// --- Project Mission messages (43.21 task 1) ------------------------------------------------
// The universal invocation path: a Project owns one Mission container, and every instruction a
// person submits becomes one child Mission Run of the Project's selected mission. Unlike the
// Project-control pair above, these produce ORDINARY runs — a run ID, a paired RunStatus, and
// run-scoped events — so Janus and Naive are indistinguishable in shape downstream.
//
// The container itself pins no mission and no capabilities. That is what lets a Project switch
// between Janus and Naive without a second container, and it is why the mission travels on the
// child command rather than on the container.

/// <summary><c>POST /conversations/project-mission</c> request. <see cref="CommandId"/> is derived
/// deterministically by Client Runtime from the stable manifest project ID through
/// <see cref="ConversationDeterministicIds.ProjectMissionContainerCreate"/>, so a retry after Host
/// acceptance but before the manifest write returns the same server-issued container ID instead of
/// creating a second one. <see cref="ProjectGoal"/> is pinned here and thereafter sourced solely
/// from the Conversation checkpoint — no run-starting message can carry or replace it.</summary>
public sealed record CreateProjectMissionContainerRequest(
    Guid ProjectId,
    Guid CommandId,
    string ProjectGoal);

/// <summary><c>POST /conversations/project-mission</c> response. A newly created container is
/// empty, so its accepted sequence is <c>0</c> — create appends no event.</summary>
public sealed record CreateProjectMissionContainerResponse(
    Guid ContainerId,
    long AcceptedSequence);

/// <summary>
/// <c>POST /conversations/{containerId}/mission-runs</c> request — start one child Mission Run.
///
/// <see cref="Mission"/> is allow-listed by Client Runtime before it is sent and again by the
/// Worker's closed catalog, so no caller can name a provider, model, expert, or arbitrary mission.
/// <see cref="CommandId"/> is generated once at submission and reused only for its retry; an equal
/// retry returns the original run, and the same ID with a different mission or input is a conflict.
///
/// There is deliberately NO capability field, and no field for a project goal, a path, a run ID, or
/// a credential. Starting a Project Mission Run grants no local tool authority: the Host declares
/// zero capabilities for every run on this route, so a direct Host caller cannot smuggle tool
/// declarations in through a message that has nowhere to put them. Removing the field is the
/// enforcement — a validation rule could be forgotten, an absent member cannot.
/// </summary>
public sealed record StartProjectMissionRunRequest(
    Guid ContainerId,
    Guid CommandId,
    string Mission,
    string Input,
    DurableMissionLaunch? Launch = null);

/// <summary><c>POST /conversations/{containerId}/mission-runs</c> response
/// (<c>202 Accepted</c>) — the same shape a Janus start already returns.</summary>
public sealed record StartProjectMissionRunResponse(
    Guid ContainerId,
    Guid RunId,
    long AcceptedSequence,
    ConversationRunStatus Status);

/// <summary>Stable, machine-readable failure returned by Project Mission routes.</summary>
public sealed record ConversationApiError(string Code, string Message);

/// <summary>One rebuildable Project Mission run summary. Input and expert output never live in this index.</summary>
public sealed record ProjectRunSummary(
    Guid RunId, Guid CommandId, string Mission, string Title,
    long AcceptedSequence, long LastSequence, ConversationRunStatus Status,
    int ExpertTurns, int ToolCalls, DateTimeOffset AcceptedAtUtc);

public sealed record ProjectRunCursor(long AnchorSequence, long BeforeAcceptedSequence);

public sealed record ProjectRunPage(
    Guid ContainerId, long IndexedSequence, long TargetSequence, bool Synchronizing,
    ProjectRunSummary[] Runs, ProjectRunCursor? Next);

public sealed record ProjectRunDetail(
    ProjectRunSummary Run, string Input, long IndexedSequence, long TargetSequence);

public sealed record ProjectRunEventPage(
    Guid ContainerId, Guid RunId, long ThroughSequence,
    long ScannedThroughSequence, ConversationEvent[] Events, bool HasMore);

public sealed record ProjectCommandReceipt(
    Guid ContainerId, Guid RunId, string Mission, string Input, string ProjectGoal,
    long AcceptedSequence, ConversationRunStatus Status);

// --- Phase 45 Mission Conversation contracts -----------------------------------------------

public sealed record CreateMissionConversationRequest(Guid ProjectId, Guid CommandId, DurableMissionLaunch Launch);
public sealed record CreateMissionConversationResponse(Guid ConversationId, long AcceptedSequence, DurableMissionLaunch Launch);
public sealed record MissionConversationSummary(Guid ConversationId, Guid ProjectId, DurableMissionLaunch Launch,
    ConversationRunStatus Status, long LastSequence, DateTimeOffset UpdatedAtUtc,
    /// <summary>The Host-owned durable display title (Phase 48). Appended last.</summary>
    string? Title = null);
public sealed record ListMissionConversationsRequest(Guid ProjectId);
public sealed record ListMissionConversationsResponse(MissionConversationSummary[] Conversations);
/// <summary>The one name a Mission Conversation carries before its first accepted turn (Phase 48).
/// Host owns the durable title rule and this is its default; it lives in the shared vocabulary so a
/// caller projecting an older stored summary uses the same single definition rather than repeating
/// the words.</summary>
public static class MissionConversationTitles
{
    public const string Default = "New chat";
}

/// <summary>One bounded, finite read of an existing Mission Conversation's ordered events
/// (Phase 48). <paramref name="Through"/> is required: a caller always states the fixed inclusive
/// upper bound it is paging toward, so a page can never silently follow a moving head. This is a
/// named bounded read, not a generic query: it carries no filter, selector, projection, or kind.
/// </summary>
public sealed record ReadMissionConversationEventsRequest(Guid ConversationId, long After, long Through);

/// <summary>One finite page of that read.</summary>
/// <param name="AfterSequence">The exclusive lower bound this page was asked for.</param>
/// <param name="RequestedThroughSequence">The caller's fixed inclusive upper bound, echoed.</param>
/// <param name="ReturnedThroughSequence">The sequence of the last event in <paramref name="Events"/>,
/// or <paramref name="AfterSequence"/> when the range held none. It is the only legal next cursor.</param>
/// <param name="HasMore">True only when durable events remain inside
/// (<paramref name="ReturnedThroughSequence"/>, <paramref name="RequestedThroughSequence"/>]. Newer
/// live events beyond the requested bound never set it, and an empty page never sets it.</param>
public sealed record MissionConversationEventPage(
    Guid ConversationId,
    ConversationEvent[] Events,
    long AfterSequence,
    long RequestedThroughSequence,
    long ReturnedThroughSequence,
    bool HasMore);

public sealed record SubmitMissionTurnRequest(Guid ConversationId, Guid CommandId, string Text);
public sealed record SubmitMissionTurnResponse(Guid ConversationId, Guid TurnId, Guid TurnAttemptId, long AcceptedSequence, ConversationRunStatus Status);
public sealed record RetryMissionTurnRequest(Guid ConversationId, Guid TurnId, Guid CommandId);
public sealed record CancelMissionTurnRequest(Guid ConversationId, Guid TurnId, Guid TurnAttemptId, Guid CommandId);
public sealed record CancelMissionTurnResponse(Guid ConversationId, Guid TurnId, Guid TurnAttemptId,
    long AcceptedSequence, ConversationRunStatus Status);

/// <summary>Host-owned hidden evaluation identity. The command ID is the Project-owned pending
/// result identity; Host uses it only for idempotency and projection lookup.</summary>
public sealed record StartEvaluationRequest(Guid ProjectId, Guid MissionId, Guid MissionVersionId, Guid EvaluationCaseId,
    Guid EvaluationResultId, int CandidateRevision, string DefinitionHash, DurableMissionLaunch Launch, string Input);
public sealed record EvaluationProjection(Guid EvaluationResultId, Guid ConversationId, Guid TurnId, Guid TurnAttemptId,
    bool IsTerminal, EvaluationOutcomeWire? ObservedOutcome, string? ObservedOutputSummary,
    EvaluationTraceOriginWire? TraceOrigin, string? Reason);
public enum EvaluationOutcomeWire { Succeeded, Failed }
public sealed record EvaluationTraceOriginWire(Guid ConversationId, Guid TurnId, Guid TurnAttemptId);
public sealed record StartEvaluationResponse(EvaluationProjection Projection);
public sealed record GetEvaluationRequest(Guid EvaluationResultId);
public sealed record GetEvaluationResponse(EvaluationProjection Projection);
