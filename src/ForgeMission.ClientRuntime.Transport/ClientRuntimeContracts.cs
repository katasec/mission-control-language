namespace ForgeMission.ClientRuntime.Transport;

// Mission: today's stateless per-prompt tool round-trip (MissionRuntimeSession/
// CloudMissionRuntimeSession). DurableConversation: the Client Runtime-owned durable Janus
// session (ConversationRuntimeSession) reached through the Task 6 Conversation API.
public enum SessionRuntimeKind
{
    Mission,
    DurableConversation,
}

// Replacement only (43.20 task 1). This request can never establish a Project's first session or
// root — only ProjectCreateRequest/ProjectOpenRequest can. ReplacesSessionId is the outgoing
// session's ID (a mission switch), so the store can cancel/dispose that session's durable tail
// instead of abandoning it, and WorkspaceRoot must equal that same session's Project home. The
// Client Runtime endpoint enforces both, so every surface — Desktop today, a TUI later — is bound
// by the rule rather than trusted to follow it.
public sealed record SessionSetupRequest(
    string WorkspaceRoot,
    string ReplacesSessionId,
    string? Mission = null,
    SessionRuntimeKind Runtime = SessionRuntimeKind.Mission);
public sealed record SessionSetupResponse(string SessionId, IReadOnlyList<string> AvailableCapabilities);

// --- Project contracts (43.20 task 1) -------------------------------------------------------
// Surface-neutral by construction: derivation, filesystem work, collision handling, and manifest
// validation all live in Client Runtime, and every expected domain failure is a typed
// ProjectOperationError rather than an exception each surface interprets its own way.

/// <summary>Side-effect-free: returns the title/home Forge would use so a surface can show them
/// before confirmation. It creates no directory, manifest, session, capability authority, or
/// collision reservation — so a concurrent creation can make <see cref="ProjectCreateRequest"/>
/// land on a different suffix than the draft displayed.</summary>
public sealed record ProjectDraftRequest(
    string Goal,
    string? TitleOverride = null,
    string? HomeOverride = null);
public sealed record ProjectDraftResponse(
    ProjectHomeProposal? Draft,
    ProjectOperationError? Error);

public sealed record ProjectCreateRequest(
    string Goal,
    string? Title = null,
    string? HomePath = null,
    string? Mission = null,
    SessionRuntimeKind Runtime = SessionRuntimeKind.Mission);

public sealed record ProjectOpenRequest(
    string HomePath,
    string? Mission = null,
    SessionRuntimeKind Runtime = SessionRuntimeKind.Mission);

/// <summary>The one response shape create and open share. Exactly one payload is populated:
/// <see cref="Session"/> for Created/Opened, <see cref="Proposal"/> for GoalRequired, and
/// <see cref="Error"/> for Failed.</summary>
public sealed record ProjectOperationResponse(
    ProjectOperationOutcome Outcome,
    ProjectSession? Session = null,
    ProjectHomeProposal? Proposal = null,
    ProjectOperationError? Error = null);

public enum ProjectOperationOutcome
{
    Created,
    Opened,
    // The chosen directory exists but holds no manifest: the same goal-confirmation flow creates
    // one there. Nothing was created to reach this outcome.
    GoalRequired,
    Failed,
}

public sealed record ProjectSession(
    string SessionId,
    IReadOnlyList<string> AvailableCapabilities,
    ProjectSummary Project);

public sealed record ProjectSummary(Guid ProjectId, string Title, string Goal, string Home);

/// <summary>What Forge would use, for display: the derived (or overridden) home and title. Returned
/// by both a draft and the GoalRequired outcome — a surface never derives either value itself.</summary>
public sealed record ProjectHomeProposal(string HomePath, string ProposedTitle);

public sealed record ProjectOperationError(ProjectOperationErrorCode Code, string Message);

// Expected Project domain failures. Unexpected process/transport failures (host down, socket
// reset) still fail the transport normally rather than being laundered into a code here.
public enum ProjectOperationErrorCode
{
    InvalidGoal,
    InvalidHome,
    HomeNotFound,
    InvalidManifest,
    UnsupportedManifestVersion,
    InvalidPath,
    CollisionAttemptsExhausted,

    // 43.20 task 2. Mission Control failures are project-scoped operation failures, so they join
    // this one vocabulary rather than introducing a second enum and error record every surface
    // would have to learn.

    /// <summary>The atomic manifest replacement failed after the Host had already accepted the
    /// control conversation. The durable conversation remains valid and the same deterministic
    /// create retry returns its ID — this is never reported as a new conversation, and never as a
    /// successful local write.</summary>
    ManifestWriteFailed,

    /// <summary>The Conversation service rejected the control request as malformed (blank IDs or
    /// text).</summary>
    MissionControlInvalid,

    /// <summary>The Conversation service reported a conflict: a reused command ID with different
    /// content, a create naming a different Project or goal, or a control message against a
    /// conversation that is not a Project-control conversation.</summary>
    MissionControlConflict,

    /// <summary>The named control conversation does not exist.</summary>
    MissionControlNotFound,

    // Phase 43.22 task 1. Appended because this enum is serialized numerically by the transport.
    ProjectBusy,
    ProjectChanged,
    ManifestReadFailed,
    SubmissionPending,
    SubmissionChanged,
    SubmissionUncertain,
    InvalidMissionInput,
    UnknownMission,
    MissionRunConflict,
    // Phase 43.22 task 3. Appended because the transport serializes this enum numerically.
    InvalidRunQuery,
    MissionRunNotFound,
    RunAlreadyActive,
    HistoryUnavailable,
    HistoryInvalid,
    HistorySynchronizing,
    DocumentUnavailable,
    DocumentTooLarge,
    DocumentChanged,
    DocumentBinary,
}

// --- Project Mission run contracts (43.22 task 3) ------------------------------------------
// The surface supplies a live session and a durable command identity. Project home, goal,
// selected mission, container and all Host details remain Runtime-owned.
public enum ProjectSubmissionState { Prepared, Accepted, Rejected }

public sealed record ProjectSubmissionView(
    Guid CommandId, string Mission, string Input, ProjectSubmissionState State,
    Guid? RunId, long? AcceptedSequence, ProjectOperationError? Rejection);

public sealed record StartProjectMissionRunRequest(
    string SessionId, Guid CommandId, Guid? PreviousCommandId, string Input);
public sealed record RetryProjectMissionSubmissionRequest(string SessionId, Guid CommandId);
public sealed record ProjectSubmissionResponse(
    ProjectSubmissionView? Submission, ProjectOperationError? Error);
public sealed record GetProjectMissionStateRequest(string SessionId);
public sealed record ProjectMissionState(
    ProjectMissionsView Missions, ProjectSubmissionView? Submission,
    ForgeMission.Conversations.Contracts.ProjectRunPage? Runs, ProjectOperationError? HistoryError);
public sealed record GetProjectMissionStateResponse(
    ProjectMissionState? State, ProjectOperationError? Error);
public sealed record GetProjectRunsRequest(string SessionId, ForgeMission.Conversations.Contracts.ProjectRunCursor? Cursor);
public sealed record GetProjectRunsResponse(ForgeMission.Conversations.Contracts.ProjectRunPage? Page, ProjectOperationError? Error);
public sealed record GetProjectRunRequest(string SessionId, Guid RunId);
public sealed record GetProjectRunResponse(ForgeMission.Conversations.Contracts.ProjectRunDetail? Run, ProjectOperationError? Error);
public sealed record GetProjectRunEventsRequest(
    string SessionId, Guid RunId, long AfterSequence, long? ThroughSequence);
public sealed record GetProjectRunEventsResponse(
    ForgeMission.Conversations.Contracts.ProjectRunEventPage? Page, ProjectOperationError? Error);

public sealed record ProjectMissionsView(
    IReadOnlyList<string> Available, string? Selected, bool HasLegacyHistory);

public sealed record SelectProjectMissionRequest(string SessionId, string Mission);
public sealed record SelectProjectMissionResponse(ProjectMissionsView? Missions, ProjectOperationError? Error);

public sealed record GetProjectWorkbenchRequest(string SessionId);
public sealed record ProjectWorkbenchEntry(string Id, string Label, string Kind);
public sealed record ProjectWorkbenchProjection(
    IReadOnlyList<ProjectWorkbenchEntry> Assets, IReadOnlyList<ProjectWorkbenchEntry> Context);
public sealed record GetProjectWorkbenchResponse(ProjectWorkbenchProjection? Projection, ProjectOperationError? Error);
public sealed record OpenProjectDocumentRequest(string SessionId, string EntryId);
public sealed record ProjectDocument(string Label, string Content, bool IsPlainText);
public sealed record OpenProjectDocumentResponse(ProjectDocument? Document, ProjectOperationError? Error);

// --- Project Mission Control contracts (43.20 task 2) ---------------------------------------
// Surface-neutral by construction, and deliberately narrow: a surface names only its own session
// and what the person typed. It supplies no Project path, no conversation ID, no project goal, no
// mission, and no capability — the Runtime resolves the Project from the session it already owns,
// reads the manifest itself, and the Conversation service sources the goal from pinned state.
// A TUI invokes both of these identically.

/// <summary>Opens the Project's one Mission Control conversation, creating it only when the
/// manifest holds no ID yet, then starts the existing durable replay/tail. Reopening a Project
/// therefore restores the same conversation without creating anything.</summary>
public sealed record OpenProjectMissionControlRequest(string SessionId);

/// <summary>Exactly one of <see cref="ConversationId"/> and <see cref="Error"/> is populated.</summary>
public sealed record OpenProjectMissionControlResponse(
    Guid? ConversationId,
    ProjectOperationError? Error = null);

/// <summary>Submits one refinement turn against the session's opened control conversation. This is
/// the TUI-equivalent action; <see cref="PromptRequest"/> remains the Janus path.
/// <see cref="CommandId"/> is generated once per user submission and reused only for its retry.</summary>
public sealed record SubmitProjectMissionControlTurnRequest(
    string SessionId,
    Guid CommandId,
    string Text);

/// <summary>Exactly one of <see cref="ConversationId"/> and <see cref="Error"/> is populated.</summary>
public sealed record SubmitProjectMissionControlTurnResponse(
    Guid? ConversationId,
    long AcceptedSequence,
    ProjectOperationError? Error = null);

public sealed record CapabilityDispatchRequest(string SessionId, CapabilityRequestData Request);
public sealed record CapabilityDispatchResponse(string Content, bool IsError);

public sealed record PromptRequest(string SessionId, string Prompt);

// ConversationId is populated only by the durable Janus path; Mission-kind sessions never set
// it. For Janus this response is an acceptance, not a synthetic chat answer — Presentation
// renders the conversation from the relayed ConversationEvent stream, not from Content.
public sealed record PromptResponse(string Content, bool IsError = false, Guid? ConversationId = null);

public sealed record ConfirmationResponseRequest(string SessionId, string ConfirmationId, bool Approved);
public sealed record ConfirmationResponse(bool Accepted);

public sealed record CapabilityRequestData(
    string CapabilityName,
    CapabilityOperation Operation,
    string? FilePath = null,
    string? Content = null,
    string? OldString = null,
    string? NewString = null,
    bool ReplaceAll = false,
    int Offset = 0,
    int? Limit = null,
    string? Command = null);

public enum CapabilityOperation
{
    ReadFile,
    EditFile,
    WriteFile,
    ExecuteTerminal,
}
