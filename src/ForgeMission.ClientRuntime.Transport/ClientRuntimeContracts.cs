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

public sealed record DefaultWorkspaceSessionRequest(
    string? Mission = null,
    SessionRuntimeKind Runtime = SessionRuntimeKind.Mission);
public sealed record DefaultWorkspaceSessionResponse(
    string SessionId,
    IReadOnlyList<string> AvailableCapabilities,
    string WorkspaceRoot);

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
