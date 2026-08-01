namespace ForgeMission.ClientRuntime.Transport;

public enum ClientRuntimeEventKind
{
    MissionTextDelta,
    ToolCallStatus,
    ConfirmationRequest,
    Error,
}

// One stable wire envelope keeps SSE deserialization simple and leaves room for transport swaps.
public sealed record ClientRuntimeEvent(
    ClientRuntimeEventKind Kind,
    string SessionId,
    string? Text = null,
    string? ToolName = null,
    string? ToolStatus = null,
    string? ConfirmationId = null,
    string? CapabilityName = null,
    string? RequestSummary = null,
    string? Error = null);
