using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ClientRuntime.Transport;

// Mission: today's stateless per-prompt tool round-trip (MissionRuntimeSession/
// CloudMissionRuntimeSession). DurableConversation: the Client Runtime-owned durable Janus
// session (ConversationRuntimeSession) reached through the Task 6 Conversation API.
public enum SessionRuntimeKind
{
    Mission,
    DurableConversation,
}

// ReplacesSessionId is the outgoing session's ID when the picker is replacing a still-live
// session (a mission switch), not creating a workspace's first session. It lets the store
// cancel/dispose the prior session's durable tail instead of abandoning it.
public sealed record SessionSetupRequest(
    string WorkspaceRoot,
    string? Mission = null,
    SessionRuntimeKind Runtime = SessionRuntimeKind.Mission,
    string? ReplacesSessionId = null);
public sealed record SessionSetupResponse(string SessionId, IReadOnlyList<string> AvailableCapabilities);

public sealed record CapabilityDispatchRequest(string SessionId, CapabilityRequestData Request);
public sealed record CapabilityDispatchResponse(string Content, bool IsError);

public sealed record PromptRequest(string SessionId, string Prompt);

// ConversationId is populated only by the durable Janus path; Mission-kind sessions never set
// it. For Janus this response is an acceptance, not a synthetic chat answer — Presentation
// renders the conversation from the relayed ConversationEvent stream, not from Content.
public sealed record PromptResponse(string Content, bool IsError = false, Guid? ConversationId = null);

public sealed record ConfirmationResponseRequest(string SessionId, string ConfirmationId, bool Approved);
public sealed record ConfirmationResponse(bool Accepted);

// Local-only, Client-Runtime-internal contracts (Phase 43.16 Task 8d) — never Host wire
// contracts. Both requests carry SessionId so the server derives WorkspaceRoot/MissionRef from
// the already-established ClientRuntimeSession; neither is ever accepted from the caller
// directly, so the Client Runtime's own UI can never list or attach a ConversationId unrelated
// to the caller's already-open workspace/mission session.
public sealed record ResumeCandidate(Guid ConversationId, string MissionRef, ConversationRunStatus Status, DateTimeOffset CreatedAtUtc);
public sealed record ResumeCandidatesRequest(string SessionId);
public sealed record ResumeCandidatesResponse(IReadOnlyList<ResumeCandidate> Candidates);
public sealed record ResumeConversationRequest(string SessionId, Guid ConversationId);
public sealed record ResumeConversationResponse(Guid ConversationId, ConversationRunStatus Status);

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
