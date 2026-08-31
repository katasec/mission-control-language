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
}

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
