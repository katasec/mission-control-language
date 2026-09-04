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
    // The zero-tool project-refinement mission's own voice (43.20 task 2). A control response is
    // labelled as itself rather than mislabelled as a Janus participant. Retained for historic
    // records only; 43.21 task 3 removes it once no legacy transcript needs reading back.
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
/// Reversing these two would make every historical conversation reactivate as
/// <see cref="ProjectControl"/> and have its next progress fact rejected by the control guard.
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
    DateTimeOffset OccurredAtUtc);

/// <summary>Compact operational checkpoint for a conversation — the projection returned by
/// <c>GET /conversations/{conversationId}</c>. The event log, not this snapshot, is canonical.</summary>
/// <param name="Purpose">Which kind of conversation this is. <see cref="Status"/>,
/// <see cref="ActiveRunId"/> and <see cref="ExpectedToolRequestId"/> describe a run lifecycle and
/// are therefore not meaningful for <see cref="ConversationPurpose.ProjectControl"/>, where they
/// stay at their initial values by construction.</param>
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
    /// <summary>The Project a <see cref="ConversationPurpose.ProjectControl"/> or
    /// <see cref="ConversationPurpose.ProjectMission"/> conversation belongs to; null otherwise.
    /// Appended last so an older snapshot's positional shape is unchanged. For a Project Mission
    /// container this is what makes existence checkable at all: it pins no mission, so a non-null
    /// Project ID paired with that purpose IS its existence invariant (43.21 task 1).</summary>
    Guid? ProjectId = null);

/// <summary>
/// Command queue body sent from the Conversation service to the Worker over the
/// <c>mission-command</c> queue. <see cref="CommandId"/> is generated by the submitting client and
/// is the queue's <c>MessageId</c>; <see cref="ConversationId"/> is the queue's <c>SessionId</c>.
/// Carries mission, goal/continuation, and capability declarations so the Worker needs no
/// conversation-store read. Contains neither credentials nor local workspace paths.
/// </summary>
/// <param name="RunId">Non-null for every <see cref="ConversationPurpose.MissionRun"/> command;
/// null for a <see cref="ConversationPurpose.ProjectControl"/> command, which has no run.</param>
/// <param name="ProjectGoal">The Project's pinned goal, supplied to the zero-tool MissionControl
/// mission on every control turn. <b>Set only by <c>ConversationGrain</c></b>, read from
/// <c>ConversationCheckpoint.ProjectGoal</c> — never caller input: no turn-submitting request or
/// grain-interface DTO has a field able to carry it, and a MissionRun command presenting a
/// non-null value is rejected as invalid. Null for every MissionRun command, so under this
/// context's <c>WhenWritingNull</c> policy a Janus command's JSON is byte-identical to before
/// this field existed.</param>
public sealed record ConversationCommand(
    Guid CommandId,
    Guid ConversationId,
    Guid? RunId,
    ConversationCommandKind Kind,
    string MissionRef,
    string Goal,
    ConversationCapabilityDeclaration[] Capabilities,
    ConversationToolResult? ToolResult,
    string? ProjectGoal = null);

/// <summary>
/// Progress queue body sent from the Worker to the Conversation service over the
/// <c>conversation-progress</c> queue. <see cref="EventId"/> is generated once by the Worker and is
/// the queue's <c>MessageId</c>; <see cref="ConversationId"/> is the queue's <c>SessionId</c>.
/// Deliberately has no sequence — the Conversation service assigns it when converting this fact
/// into the canonical <see cref="ConversationEvent"/> through the grain.
/// </summary>
/// <param name="RunId">Non-null for a <see cref="ConversationPurpose.MissionRun"/> fact; null for
/// a <see cref="ConversationPurpose.ProjectControl"/> fact, which belongs to no run.</param>
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
    DateTimeOffset OccurredAtUtc);

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

// --- Project-control messages (43.20 task 2) ------------------------------------------------
// A Project's Mission Control conversation is a zero-tool refinement conversation, deliberately
// NOT the Janus start/follow-up path: StartConversationRequest pins a MissionRef and capability
// declarations and returns a RunId, and every SubmitConversationCommandRequest becomes a new
// StartMission command. Reusing either for ordinary Project refinement would silently start Janus
// work and permit the wrong capability shape. These two messages therefore have no field able to
// carry a capability, local path, tool, selected launch mission, credential, or run ID — a caller
// cannot supply one even by hand-crafting the request body.

/// <summary><c>POST /conversations/project-control</c> request. <see cref="CommandId"/> is derived
/// deterministically by Client Runtime from the stable manifest project ID through
/// <see cref="ConversationDeterministicIds.ProjectControlCreate"/>, so a retry after Host
/// acceptance but before the manifest write returns the same server-issued conversation ID.
/// This is the ONLY message that carries <see cref="ProjectGoal"/>; it is pinned on acceptance and
/// thereafter sourced solely from the Conversation checkpoint.</summary>
public sealed record CreateProjectControlConversationRequest(
    Guid ProjectId,
    Guid CommandId,
    string ProjectGoal);

/// <summary><c>POST /conversations/project-control</c> response. A newly created, still-empty
/// control conversation returns <see cref="AcceptedSequence"/> <c>0</c> — create appends no
/// event.</summary>
public sealed record CreateProjectControlConversationResponse(
    Guid ConversationId,
    long AcceptedSequence);

/// <summary><c>POST /conversations/{conversationId}/control-messages</c> request. <see cref="CommandId"/>
/// is generated once per user submission and reused only for its retry; a duplicate carrying
/// different <see cref="Text"/> is a conflict. Deliberately has no project-goal field: the goal
/// reaches the mission from pinned checkpoint state, never from the submitter.</summary>
public sealed record SubmitProjectControlMessageRequest(
    Guid ConversationId,
    Guid CommandId,
    string Text);

/// <summary><c>POST /conversations/{conversationId}/control-messages</c> response
/// (<c>202 Accepted</c>) — the sequence of the single appended <c>UserMessage</c>.</summary>
public sealed record SubmitProjectControlMessageResponse(
    Guid ConversationId,
    long AcceptedSequence);

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
/// <see cref="Capabilities"/> is per-run rather than pinned on the container: a Janus run declares
/// the session's capabilities, a Naive run declares none.
/// <see cref="CommandId"/> is generated once at submission and reused only for its retry; an equal
/// retry returns the original run, and the same ID with a different mission or input is a conflict.
/// There is deliberately no field for a project goal, a path, a run ID, or a credential.
/// </summary>
public sealed record StartProjectMissionRunRequest(
    Guid ContainerId,
    Guid CommandId,
    string Mission,
    string Input,
    ConversationCapabilityDeclaration[] Capabilities);

/// <summary><c>POST /conversations/{containerId}/mission-runs</c> response
/// (<c>202 Accepted</c>) — the same shape a Janus start already returns.</summary>
public sealed record StartProjectMissionRunResponse(
    Guid ContainerId,
    Guid RunId,
    long AcceptedSequence,
    ConversationRunStatus Status);
