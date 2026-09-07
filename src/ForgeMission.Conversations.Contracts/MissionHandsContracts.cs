using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForgeMission.Conversations.Contracts;

/// <summary>Closed durable profile vocabulary.  A caller can acknowledge but cannot select it.</summary>
public enum MissionHandsProfile
{
    [JsonStringEnumMemberName("noHands")] NoHands,
    [JsonStringEnumMemberName("projectWorkspace")] ProjectWorkspace,
    [JsonStringEnumMemberName("projectWorkspaceAndTerminal")] ProjectWorkspaceAndTerminal,
}

/// <summary>Opaque, profile-pinned mission provenance retained by the Conversation owner.</summary>
public sealed record DurableMissionLaunch(
    Guid MissionVersionId,
    int VersionNumber,
    string DefinitionHash,
    string Definition,
    MissionHandsProfile Profile);

public enum MissionHandsStatus
{
    [JsonStringEnumMemberName("attached")] Attached,
    [JsonStringEnumMemberName("awaitingHands")] AwaitingHands,
    [JsonStringEnumMemberName("awaitingToolConfirmation")] AwaitingToolConfirmation,
    [JsonStringEnumMemberName("inFlight")] InFlight,
    [JsonStringEnumMemberName("completed")] Completed,
    [JsonStringEnumMemberName("cancelled")] Cancelled,
    [JsonStringEnumMemberName("interrupted")] Interrupted,
}

public enum MissionToolOutcome
{
    [JsonStringEnumMemberName("succeeded")] Succeeded,
    [JsonStringEnumMemberName("deniedOutOfProfile")] DeniedOutOfProfile,
    [JsonStringEnumMemberName("deniedByPolicy")] DeniedByPolicy,
    [JsonStringEnumMemberName("deniedByOperator")] DeniedByOperator,
    [JsonStringEnumMemberName("cancelled")] Cancelled,
    [JsonStringEnumMemberName("failed")] Failed,
    [JsonStringEnumMemberName("interrupted")] Interrupted,
}

/// <summary>Contains no local root, credential, Bob handle, or Worker transcript.</summary>
public sealed record MissionToolRequest(
    Guid ToolRequestId,
    Guid ConversationId,
    Guid TurnAttemptId,
    string MissionLocation,
    string AgentLocation,
    string ProviderToolCallId,
    string ToolName,
    JsonElement Arguments,
    string OpaqueContinuation);

public sealed record MissionHandsAttachment(
    Guid ConversationId,
    Guid AttachmentId,
    Guid ApplicationSessionId,
    DurableMissionLaunch Launch,
    DateTimeOffset AttachedAtUtc);

public sealed record AttachMissionHandsRequest(
    Guid ConversationId, Guid AttachmentId, Guid ApplicationSessionId, DurableMissionLaunch Launch);
public sealed record DetachMissionHandsRequest(Guid ConversationId, Guid AttachmentId);
public sealed record SubmitMissionToolResultRequest(
    Guid ConversationId, Guid AttachmentId, Guid TurnAttemptId, Guid CommandId, Guid ToolRequestId,
    MissionToolOutcome Outcome, string? Content, string? Reason);
/// <summary>Application can read work only when this exact fresh attachment/session pair is
/// live. The request never travels through Presentation or Application transport.</summary>
public sealed record GetMissionHandsWorkRequest(
    Guid ConversationId, Guid AttachmentId, Guid ApplicationSessionId);
public sealed record ClaimMissionHandsWorkRequest(
    Guid ConversationId, Guid AttachmentId, Guid ApplicationSessionId);
public sealed record MissionHandsWorkItem(
    MissionHandsStatus Status, MissionHandsAttachment? Attachment, MissionToolRequest? Request,
    long? Sequence, string? Reason = null);
public sealed record BeginMissionHandsConfirmationRequest(
    Guid ConversationId, Guid AttachmentId, Guid ToolRequestId);
public sealed record CancelMissionHandsAttemptRequest(
    Guid ConversationId, Guid AttachmentId, Guid TurnAttemptId, Guid ToolRequestId, string Reason);
/// <summary>A fresh pending attachment terminalizes an unknown old in-flight operation. Host
/// derives the old request and fixed reason; callers cannot select a result or continuation.</summary>
public sealed record RecoverMissionHandsInFlightRequest(
    Guid ConversationId, Guid AttachmentId, Guid ApplicationSessionId);
public sealed record MissionHandsResult(
    MissionHandsStatus Status, long? AcceptedSequence, string? Reason = null);
