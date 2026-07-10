namespace ForgeUI.Models;

/// <summary>
/// Boundary-shaped room message — what the ChatHub broadcasts and the room view
/// renders. EF entities never cross SignalR; mapping is manual (RoomsMappings).
/// <see cref="Trust"/> is null for human messages and carries the trust surface
/// (badge + trace) for agent messages (38.3).
/// </summary>
public record RoomMessageDto(
    Guid Id,
    Guid RoomId,
    Guid SenderId,
    string SenderName,
    string SenderKind,
    string? Text,
    Guid? ReplyTo,
    DateTimeOffset CreatedAt,
    AgentTrustDto? Trust = null,
    IReadOnlyList<ArtifactDto>? Artifacts = null);

/// <summary>
/// A downloadable file attached to a message (38.9) — metadata only; bytes are fetched on demand
/// from the membership-gated <c>GET /rooms/{roomId}/artifacts/{id}</c> endpoint. Never carries bytes
/// across SignalR.
/// </summary>
public record ArtifactDto(
    Guid Id,
    string Filename,
    string ContentType,
    long Size);

/// <summary>
/// Trust surface for an agent message. <see cref="Verified"/> is already run through the
/// no-false-green guard at mapping time — the render layer renders it, never re-derives it.
/// </summary>
public record AgentTrustDto(
    string Handle,
    bool Verified,
    int StepCount,
    int RetryCount,
    List<AgentTraceStepDto> Trace);

public record AgentTraceStepDto(
    string ExpertName,
    string Status,
    string? Text,
    string? Reason,
    int Attempt);
