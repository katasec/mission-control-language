using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ClientRuntime.Transport;

public enum ClientRuntimeEventKind
{
    MissionTextDelta,
    ToolCallStatus,
    ConfirmationRequest,
    Error,
    ConversationEvent,
    ProjectMissionChanged,
}

// One stable wire envelope keeps SSE deserialization simple and leaves room for transport swaps.
// Conversation carries one complete durable ConversationEvent (relayed verbatim by
// ConversationRuntimeSession) whenever Kind is ConversationEvent; every other kind leaves it
// null. Serialized/deserialized through ConversationRelayJsonContext, not ClientRuntimeJsonContext
// — see that file for why.
public sealed record ClientRuntimeEvent(
    ClientRuntimeEventKind Kind,
    string SessionId,
    string? Text = null,
    string? ToolName = null,
    string? ToolStatus = null,
    string? ToolTarget = null,
    string? ConfirmationId = null,
    string? CapabilityName = null,
    string? RequestSummary = null,
    string? Error = null,
    ConversationEvent? Conversation = null,
    ProjectMissionChange? ProjectMission = null);

public sealed record ProjectMissionChange(Guid ContainerId, long LastSequence);
