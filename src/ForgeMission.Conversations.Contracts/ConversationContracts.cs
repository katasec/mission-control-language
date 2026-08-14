using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForgeMission.Conversations.Contracts;

// Durable conversation wire contracts (Phase 43.16 Task 2). Data and project-seam only: no
// endpoint, grain, queue, or MCL-execution behaviour lives here. Shared by ConversationHost (the
// server/Silo) and, from Task 7, ForgeMission.ClientRuntime — so every type here must stay
// AOT-safe and free of any Host/Orleans/Azure/provider dependency. See
// docs/design/durable-conversations.md and docs/phases/phase-43.16-janus-desktop-local-poc.md.

/// <summary>Who produced a <see cref="ConversationEvent"/> or <see cref="ConversationProgress"/> fact.</summary>
public enum ConversationParticipant
{
    [JsonStringEnumMemberName("user")]        User,
    [JsonStringEnumMemberName("proposer")]    Proposer,
    [JsonStringEnumMemberName("approver")]    Approver,
    [JsonStringEnumMemberName("implementer")] Implementer,
    [JsonStringEnumMemberName("forge")]       Forge,
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
public sealed record ConversationSnapshot(
    Guid ConversationId,
    string MissionRef,
    Guid? ActiveRunId,
    long LastSequence,
    ConversationRunStatus Status,
    Guid? ExpectedToolRequestId,
    DateTimeOffset UpdatedAtUtc);

/// <summary>
/// Command queue body sent from the Conversation service to the Worker over the
/// <c>mission-command</c> queue. <see cref="CommandId"/> is generated by the submitting client and
/// is the queue's <c>MessageId</c>; <see cref="ConversationId"/> is the queue's <c>SessionId</c>.
/// Carries mission, goal/continuation, and capability declarations so the Worker needs no
/// conversation-store read. Contains neither credentials nor local workspace paths.
/// </summary>
public sealed record ConversationCommand(
    Guid CommandId,
    Guid ConversationId,
    Guid RunId,
    ConversationCommandKind Kind,
    string MissionRef,
    string Goal,
    ConversationCapabilityDeclaration[] Capabilities,
    ConversationToolResult? ToolResult);

/// <summary>
/// Progress queue body sent from the Worker to the Conversation service over the
/// <c>conversation-progress</c> queue. <see cref="EventId"/> is generated once by the Worker and is
/// the queue's <c>MessageId</c>; <see cref="ConversationId"/> is the queue's <c>SessionId</c>.
/// Deliberately has no sequence — the Conversation service assigns it when converting this fact
/// into the canonical <see cref="ConversationEvent"/> through the grain.
/// </summary>
public sealed record ConversationProgress(
    Guid EventId,
    Guid ConversationId,
    Guid RunId,
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
