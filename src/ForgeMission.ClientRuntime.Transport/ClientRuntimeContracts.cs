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

    // 43.20 task 3. Appended at the end deliberately: this enum serializes numerically under
    // ClientRuntimeJsonContext, so inserting a member anywhere else would silently renumber every
    // code after it and change what an existing surface reads off the wire.

    /// <summary>A recorded expert dependency cannot be presented honestly: a malformed source URI,
    /// a content digest that does not match the resolved file, or a source whose derived
    /// materialization is absent. The whole projection fails rather than showing a partial or
    /// invented dependency list.</summary>
    InvalidDependency,

    /// <summary>The named entry is not in the Project's current projection — unknown, or stale
    /// because the Project changed since it was listed.</summary>
    DocumentNotFound,

    /// <summary>The entry exists but is not presentable as text: binary content, invalid UTF-8, or
    /// larger than 1 MiB.</summary>
    InvalidDocument,

    // 43.21 task 1. Appended for the same wire-stability reason as the codes above.

    /// <summary>A mission outside the closed Janus/Naive catalog was selected or started — or a
    /// hand-edited manifest names one. Nothing is persisted and no run is created; the selection
    /// is never silently defaulted to Janus, because running work nobody chose is worse than
    /// refusing.</summary>
    UnknownMission,

    /// <summary>The instruction is missing, blank, or larger than the accepted bound. No run is
    /// created.</summary>
    InvalidMissionInput,

    /// <summary>This Project already has a Mission Run that is queued, running, or awaiting a
    /// tool. The MVP runs one at a time; the submission creates no run and appends no event.</summary>
    RunAlreadyActive,

    /// <summary>The Conversation service refused the run as conflicting — a reused command id with
    /// different content, or a container that is not this Project's.</summary>
    MissionRunConflict,

    /// <summary>The Project's Mission container could not be found.</summary>
    MissionRunNotFound,
}

// --- Project Mission contracts (43.21 task 1) ------------------------------------------------
// The universal invocation path: a Project's selected mission is persistent and Project-scoped,
// and every instruction a person submits starts one child Mission Run of it. Presentation never
// branches execution — there is no field here through which it could name a provider, a model, an
// expert, a path, or a run, and the mission a run uses is read from the Project rather than sent.
// A TUI invokes both of these identically.

/// <summary>Persists the Project's mission selection. Client Runtime allow-lists the value against
/// the closed Janus/Naive catalog before writing and returns the canonical result, so a surface
/// renders what was actually stored rather than what it asked for.</summary>
public sealed record SelectProjectMissionRequest(string SessionId, string Mission);

/// <summary>Exactly one of <see cref="SelectedMission"/> and <see cref="Error"/> is populated.</summary>
public sealed record SelectProjectMissionResponse(
    string? SelectedMission,
    ProjectOperationError? Error = null);

/// <summary>Starts one child Mission Run of the Project's selected mission.
/// <see cref="CommandId"/> is generated once when the person submits and reused only for its
/// retry: an equal retry returns the original run, and the same ID with different input is a typed
/// conflict rather than a second run. <see cref="Input"/> is the instruction and is deliberately
/// distinct from the Project goal, which the Runtime derives.</summary>
public sealed record StartProjectMissionRunRequest(string SessionId, Guid CommandId, string Input);

/// <summary>Exactly one of <see cref="RunId"/> and <see cref="Error"/> is populated.
/// <see cref="Mission"/> names the mission that was actually started, so a surface reports
/// "Starting Janus…" from the durable answer rather than from its own local state.</summary>
public sealed record StartProjectMissionRunResponse(
    Guid? RunId,
    string? Mission = null,
    long AcceptedSequence = 0,
    ProjectOperationError? Error = null);

// --- Project workbench contracts (43.20 task 3) ----------------------------------------------
// Surface-neutral and deliberately path-free: a surface names its own session and, to open a
// document, an entry ID the Runtime itself minted. It never supplies a path, an OCI reference, or
// a registry credential, and none of those ever comes back. Reading the manifest and the lock
// file, validating every source URI, deriving a materialization location, and validating document
// content all live below this boundary.

/// <summary>Reads the open Project's Explorer projection. Purely a read: it opens no registry
/// connection, resolves no dependency, and changes no local state.</summary>
public sealed record GetProjectWorkbenchRequest(string SessionId);

/// <summary>Exactly one of <see cref="Workbench"/> and <see cref="Error"/> is populated.</summary>
public sealed record GetProjectWorkbenchResponse(
    ProjectWorkbenchProjection? Workbench,
    ProjectOperationError? Error = null);

public sealed record ProjectWorkbenchProjection(
    ProjectSummary Project,
    IReadOnlyList<ProjectExplorerEntry> Assets,
    IReadOnlyList<ProjectExplorerEntry> Context,
    IReadOnlyList<ProjectExplorerEntry> Runs);

/// <summary>One listed item. <see cref="EntryId"/> is opaque and stable for the same Project state:
/// a surface only ever hands it back, and the Runtime resolves it by matching a freshly built
/// projection rather than by interpreting it as a location. <see cref="Source"/> is populated only
/// for a resolved OCI dependency, where the pinned reference and digest ARE the evidence being
/// shown; it is never a local path.</summary>
public sealed record ProjectExplorerEntry(
    string EntryId,
    string DisplayName,
    ProjectExplorerEntryKind Kind,
    bool IsReadOnly,
    string? Source = null);

public enum ProjectExplorerEntryKind
{
    Mission,
    Expert,
    LockFile,
    SourceRoot,
    File,
    Artifact,
    Run,

    /// <summary>An expert resolved from a registry and pinned to one immutable manifest digest.
    /// Read-only dependency evidence, like an installed package — never an editable Project
    /// asset.</summary>
    OciDependency,
}

/// <summary>Opens one entry the projection returned. It accepts no arbitrary path and no OCI
/// reference, so there is no input a surface could widen into a file read.</summary>
public sealed record OpenProjectDocumentRequest(string SessionId, string EntryId);

/// <summary>Exactly one of <see cref="Document"/> and <see cref="Error"/> is populated.</summary>
public sealed record OpenProjectDocumentResponse(
    ProjectDocument? Document,
    ProjectOperationError? Error = null);

/// <summary>Text only, and never the location it came from.</summary>
public sealed record ProjectDocument(string Title, string ContentType, string Text);

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
