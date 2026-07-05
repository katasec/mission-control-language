namespace ForgeMission.Rooms.Data;

/// <summary>
/// Write-side of the data-access seam — backed by the WriteConnection slot.
/// Messages are append-only: there is deliberately no update/delete surface.
/// </summary>
public interface IWriteStore
{
    Task<Member> AddMemberAsync(Member member, CancellationToken ct = default);

    Task<Room> AddRoomAsync(Room room, CancellationToken ct = default);

    Task<RoomMembership> AddMembershipAsync(RoomMembership membership, CancellationToken ct = default);

    /// <summary>INSERT only — messages are immutable once appended.</summary>
    Task<Message> AppendMessageAsync(Message message, CancellationToken ct = default);
}
