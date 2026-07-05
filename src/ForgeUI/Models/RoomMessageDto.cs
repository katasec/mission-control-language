namespace ForgeUI.Models;

/// <summary>
/// Boundary-shaped room message — what the ChatHub broadcasts and the room view
/// renders. EF entities never cross SignalR; mapping is manual (RoomsMappings).
/// </summary>
public record RoomMessageDto(
    Guid Id,
    Guid RoomId,
    Guid SenderId,
    string SenderName,
    string SenderKind,
    string? Text,
    Guid? ReplyTo,
    DateTimeOffset CreatedAt);
