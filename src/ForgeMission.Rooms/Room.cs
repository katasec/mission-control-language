namespace ForgeMission.Rooms;

/// <summary>
/// The multi-party primitive. 1:1 is a room of two members, one possibly an agent.
/// Access control hangs off <see cref="RoomMembership"/>, never off this type.
/// </summary>
public sealed class Room
{
    public Guid Id { get; set; }
    public RoomMetadata Metadata { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
}
