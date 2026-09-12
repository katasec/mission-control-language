namespace ForgeMission.Application.Transport;

// Mission: today's stateless per-prompt tool round-trip (LegacyMissionProtocolClient/
// LegacyCloudMissionProtocolClient). DurableConversation: the Application-owned Janus
// ConversationService/ConversationScope reached through the Task 6 Conversation API.
public enum SessionRuntimeKind
{
    Mission,
    DurableConversation,
}

// Replacement only (43.20 task 1). This request can never establish a Project's first session or
// root — only ProjectCreateRequest/ProjectOpenRequest can. ReplacesSessionId is the outgoing
// session's ID (a mission switch), so the store can cancel/dispose that session's durable tail
// instead of abandoning it, and WorkspaceRoot must equal that same session's Project home. The
// Application endpoint enforces both, so every surface — Desktop today, a TUI later — is bound
// by the rule rather than trusted to follow it.
public sealed record SessionSetupRequest(
    string WorkspaceRoot,
    string ReplacesSessionId,
    string? Mission = null,
    SessionRuntimeKind Runtime = SessionRuntimeKind.Mission);
public sealed record SessionSetupResponse(string SessionId, IReadOnlyList<string> AvailableCapabilities);

// --- Project contracts (43.20 task 1) -------------------------------------------------------
// Surface-neutral by construction: derivation, filesystem work, collision handling, and manifest
// validation all live in Application, and every expected domain failure is a typed
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

    /// <summary>The atomic manifest replacement failed after a durable Host acceptance. The
    /// durable record remains valid; no local write is reported as successful.</summary>
    ManifestWriteFailed,

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
    // Phase 45.1: intentionally append-only; evaluation execution arrives with 45.2.
    EvaluationUnavailable,
    VersionChanged,
    PublishConflict,
}

// --- Project Mission run contracts (43.22 task 3) ------------------------------------------
// The surface supplies a live session and a durable command identity. Project home, goal,
// selected mission, container and all Host details remain Runtime-owned.
public enum ProjectSubmissionState { Prepared, Accepted, Rejected }

public sealed record ProjectSubmissionView(
    Guid CommandId, string Mission, string Input, ProjectSubmissionState State,
    Guid? RunId, long? AcceptedSequence, ProjectOperationError? Rejection);

public sealed record StartProjectMissionRunRequest(
    string SessionId, Guid CommandId, Guid? PreviousCommandId, string Input,
    bool ProfileAccepted = false);
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

public sealed record CapabilityDispatchRequest(string SessionId, CapabilityRequestData Request);
public sealed record CapabilityDispatchResponse(string Content, bool IsError);

public sealed record PromptRequest(string SessionId, string Prompt);

// ConversationId is populated only by the durable Janus path; Mission-kind sessions never set
// it. For Janus this response is an acceptance, not a synthetic chat answer — Presentation
// renders the conversation from the relayed ConversationEvent stream, not from Content.
public sealed record PromptResponse(string Content, bool IsError = false, Guid? ConversationId = null);

public sealed record ConfirmationResponseRequest(string SessionId, string ConfirmationId, bool Approved);
public sealed record ConfirmationResponse(bool Accepted);

// Generic mission hands lifecycle. ProfileAccepted is an acknowledgement of the resolved launch
// shown by a surface; it is never a caller-selected capability profile or tool declaration.
public sealed record AcknowledgeMissionHandsRequest(
    string SessionId, Guid ConversationId, Guid MissionVersionId, int VersionNumber,
    string DefinitionHash, bool ProfileAccepted);
public sealed record AcknowledgeMissionHandsResponse(
    Guid? AttachmentId, string? Profile, IReadOnlyList<string>? AvailableCapabilities, string? Error);
public sealed record DetachMissionHandsRequest(string SessionId, Guid ConversationId, Guid AttachmentId);
public sealed record DetachMissionHandsResponse(bool Detached, string? Error);
/// <summary>Reads the locally held attachment state.  This is intentionally an Application
/// projection: a surface never reads Bob or the Conversation Host directly.</summary>
public sealed record GetMissionHandsStatusRequest(string SessionId, Guid ConversationId, Guid AttachmentId);
public sealed record GetMissionHandsStatusResponse(
    Guid? AttachmentId, string? Profile, IReadOnlyList<string>? AvailableCapabilities,
    ForgeMission.Conversations.Contracts.MissionHandsStatus? Status, string? Error);
public sealed record ExecuteMissionHandsRequest(
    string SessionId, Guid ConversationId, Guid AttachmentId);
public sealed record ExecuteMissionHandsResponse(
    ForgeMission.Conversations.Contracts.MissionToolOutcome? Outcome, string? Content, string? Reason, long? AcceptedSequence, string? Error);
public sealed record CancelMissionHandsRequest(string SessionId, Guid ConversationId, Guid AttachmentId, string Reason);
public sealed record CancelMissionHandsResponse(bool Cancelled, long? AcceptedSequence, string? Error);
/// <summary>Explicitly terminalizes a Host-owned operation whose old Bob could not durably
/// detach. It carries correlation only; Host derives the interrupted request and outcome.</summary>
public sealed record RecoverMissionHandsRequest(string SessionId, Guid ConversationId, Guid AttachmentId);
public sealed record RecoverMissionHandsResponse(
    ForgeMission.Conversations.Contracts.MissionHandsStatus? Status, long? AcceptedSequence, string? Error);

// --- Missions landing contracts (45.3 task 3A) ----------------------------------------------
// The rendered states own this vocabulary and nothing more. No request can name a package,
// definition, profile, capability, tool, Project path, launch, or attachment: a surface asks for
// a mission, and Application alone decides which approved version that resolves to.

public sealed record ListMissionConversationsRequest(string SessionId);

/// <summary>One informational row. Conversation identity, pinned version number and update time
/// are Host facts; <see cref="MissionName"/> is the Project's local display identity, resolved by
/// the pinned version ID. It is null when this Project no longer holds that version, which the
/// surface states plainly rather than inventing a name or hiding the Host's record.</summary>
public sealed record MissionConversationListItem(
    Guid ConversationId, string? MissionName, int VersionNumber, DateTimeOffset UpdatedAtUtc);
public sealed record ListMissionConversationsResponse(
    IReadOnlyList<MissionConversationListItem>? Conversations, ProjectOperationError? Error);

public sealed record ListApprovedMissionVersionsRequest(string SessionId);

/// <summary>A selectable option. It carries the identity and the exact profile a surface must
/// show, and deliberately no definition text or package: displaying an approved version is not
/// the same as holding what executes it.</summary>
public sealed record ApprovedMissionVersionOption(
    Guid MissionId, string MissionName, Guid MissionVersionId, int VersionNumber,
    string DefinitionHash, ForgeMission.Conversations.Contracts.MissionHandsProfile Profile);
public sealed record ListApprovedMissionVersionsResponse(
    IReadOnlyList<ApprovedMissionVersionOption>? Options, ProjectOperationError? Error);

/// <summary><see cref="ExpectedMissionVersionId"/> is a stale-display precondition, never a
/// selection: it is the version the operator actually read before approving, and Application
/// rejects the create with <see cref="ProjectOperationErrorCode.VersionChanged"/> when the
/// mission's current approved version is a different one. It cannot name a version to use, so an
/// operator can never be pinned to — or asked to acknowledge — access they never saw.</summary>
public sealed record CreateMissionConversationRequest(
    string SessionId, Guid MissionId, Guid CommandId, Guid ExpectedMissionVersionId);

/// <summary>What Application resolved and Host pinned. The surface echoes this exact value back
/// when it acknowledges; it composes no approval of its own.</summary>
public sealed record MissionAccessApproval(
    Guid MissionVersionId, int VersionNumber, string DefinitionHash,
    ForgeMission.Conversations.Contracts.MissionHandsProfile Profile);
public sealed record CreatedMissionConversation(Guid ConversationId, MissionAccessApproval Approval);
public sealed record CreateMissionConversationResponse(
    CreatedMissionConversation? Created, ProjectOperationError? Error);

// --- Mission Chat contracts (Phase 48) ------------------------------------------------------
// The live Mission Chat journey's whole vocabulary. Every action is concrete and derives nothing
// from its caller: no request names a package, definition, profile, capability, tool, Project path,
// launch, attachment, title, or version, and no response carries a capability list, a cursor, a page
// size, or a partial-history flag. Application assembles a chat's complete history before it hands
// one back, so a surface never pages and never has to render "some of it".

/// <summary>Zero input by design: Forge resolves or provisions the one managed chat Project, pins
/// the shipped version, and creates the chat. A person picks no folder, Project, mission, or
/// version here.</summary>
public sealed record StartMissionChatRequest();

/// <summary>One further chat in the session's managed Project.</summary>
public sealed record CreateMissionChatRequest(string SessionId);

public sealed record OpenMissionChatRequest(string SessionId, Guid ConversationId);

public sealed record SubmitMissionChatTurnRequest(
    string SessionId, Guid ConversationId, Guid CommandId, string Text);

/// <summary>A chat's read-only provenance. <paramref name="VersionLabel"/> is display text only — a
/// shipped version's release label, or <c>v&lt;number&gt;</c> for an authored one; admission and
/// pinning use the identity Application already holds.</summary>
public sealed record MissionChatPin(
    string MissionName, int VersionNumber, string VersionLabel,
    ForgeMission.Conversations.Contracts.MissionHandsProfile Profile, bool IsApproved);

/// <summary>One real chat row. It exists only because a durable conversation does.
/// <paramref name="Title"/> is the Host-owned durable title; <paramref name="MissionName"/> is null
/// when this Project no longer holds the pinned version, which the surface states plainly rather
/// than inventing a name.</summary>
public sealed record MissionChatRow(
    Guid ConversationId, string Title, string? MissionName, int VersionNumber, string? VersionLabel,
    DateTimeOffset UpdatedAtUtc, bool HasMessages);

/// <summary>Whether the version's fixed profile is attached right now. It is informational: this
/// flow never asks for or records profile consent.</summary>
public sealed record MissionChatAccess(MissionChatAccessState State, string? Reason);

public enum MissionChatAccessState { Attached, Unavailable }

public sealed record MissionChatFailure(MissionChatFailureCode Code, string Message);

public enum MissionChatFailureCode
{
    ManagedProjectInvalid,
    MissionUnavailable,
    ChatNotFound,
    TurnConflict,
    CreateUncertain,
    HistoryUnavailable,
    HistoryProtocol,
    StreamLost,
}

/// <summary><paramref name="Events"/> is the chat's complete ordered history through
/// <paramref name="ThroughSequence"/>, which is also the sequence the live stream resumes from, so a
/// surface applies one list and then live events with nothing in between.</summary>
public sealed record StartMissionChatResponse(
    ProjectSession? Session, Guid? ConversationId, MissionChatPin? Pin, MissionChatAccess? Access,
    IReadOnlyList<MissionChatRow>? Rows, IReadOnlyList<ForgeMission.Conversations.Contracts.ConversationEvent>? Events,
    long ThroughSequence, MissionChatFailure? Failure);

/// <summary>The same shape, from the session that is already open: it carries no Project session
/// because it never opens one.</summary>
public sealed record CreateMissionChatResponse(
    Guid? ConversationId, MissionChatPin? Pin, MissionChatAccess? Access,
    IReadOnlyList<MissionChatRow>? Rows, IReadOnlyList<ForgeMission.Conversations.Contracts.ConversationEvent>? Events,
    long ThroughSequence, MissionChatFailure? Failure);

public sealed record OpenMissionChatResponse(
    Guid? ConversationId, MissionChatPin? Pin, MissionChatAccess? Access,
    IReadOnlyList<MissionChatRow>? Rows, IReadOnlyList<ForgeMission.Conversations.Contracts.ConversationEvent>? Events,
    long ThroughSequence, MissionChatFailure? Failure);

public sealed record SubmitMissionChatTurnResponse(
    Guid? TurnId, long? AcceptedSequence,
    ForgeMission.Conversations.Contracts.ConversationRunStatus? Status, MissionChatFailure? Failure);

// --- Mission authoring contracts (45.4 minimum) ---------------------------------------------
// The smallest vocabulary that lets one operator author, evaluate and publish. Projects still
// owns the lifecycle: CanPublish below is a projection of what it already enforces, and
// PublishMissionVersion is refused there too, so a surface that ignored the flag changes nothing.

public enum MissionVersionStateView { Candidate, Evaluated, Approved, Superseded }
public enum MissionEditableKind { None, Draft, Candidate }
public enum EvaluationOutcomeView { Succeeded, Failed }
public enum EvaluationResultStateView { None, Pending, Passed, Failed }

public sealed record MissionDefinitionSummary(
    Guid MissionId, string Name, int? LatestVersionNumber, MissionVersionStateView? LatestState, bool HasDraft);

/// <summary><see cref="TraceReference"/> is evidence a result came from a real durable run. It is
/// an identifier to read, not a link: the trace surface is a later task.</summary>
public sealed record EvaluationCaseView(
    Guid EvaluationCaseId, string Input, string ExpectedSuccess, string ExpectedFailure,
    EvaluationOutcomeView ExpectedOutcome, IReadOnlyList<string> RequiredFragments,
    EvaluationResultStateView ResultState, string? ObservedSummary, string? TraceReference);

public sealed record MissionAuthoringDocument(
    Guid MissionId, string Name, MissionEditableKind Editable,
    ForgeMission.Conversations.Contracts.MissionHandsProfile Profile,
    Guid? DraftId, Guid? MissionVersionId, int Revision, int? VersionNumber,
    string DefinitionText, IReadOnlyList<EvaluationCaseView> Cases,
    bool CanPublish, string? PublishBlockedReason);

public sealed record MissionAuthoringProjection(
    IReadOnlyList<MissionDefinitionSummary> Missions, MissionAuthoringDocument? Open);

public sealed record GetMissionAuthoringRequest(string SessionId, Guid? MissionId);
public sealed record GetMissionAuthoringResponse(MissionAuthoringProjection? Authoring, ProjectOperationError? Error);

/// <summary>One response for every mutation: the refusal, and the document as it actually stands
/// beside it, so a surface never has to guess what a rejected action left behind.</summary>
public sealed record MissionAuthoringMutationResponse(
    MissionAuthoringProjection? Authoring, ProjectOperationError? Error);

public sealed record CreateMissionDraftRequest(
    string SessionId, string Name, string DefinitionText,
    ForgeMission.Conversations.Contracts.MissionHandsProfile Profile);
public sealed record SaveMissionDraftRequest(
    string SessionId, Guid MissionId, Guid DraftId, int Revision, string DefinitionText,
    ForgeMission.Conversations.Contracts.MissionHandsProfile Profile);
public sealed record PromoteMissionCandidateRequest(string SessionId, Guid MissionId, Guid DraftId, int Revision);

public sealed record EvaluationCaseInput(
    string Input, string? ExpectedSuccess, string? ExpectedFailure,
    EvaluationOutcomeView ExpectedOutcome, IReadOnlyList<string>? RequiredFragments);

public sealed record AddEvaluationCaseRequest(
    string SessionId, Guid MissionId, Guid MissionVersionId, EvaluationCaseInput Case);
public sealed record UpdateEvaluationCaseRequest(
    string SessionId, Guid MissionId, Guid MissionVersionId, Guid EvaluationCaseId, EvaluationCaseInput Case);
public sealed record RunEvaluationCaseRequest(
    string SessionId, Guid MissionId, Guid MissionVersionId, Guid EvaluationCaseId);
public sealed record PublishMissionVersionRequest(string SessionId, Guid MissionId, Guid MissionVersionId);

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
