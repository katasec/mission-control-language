namespace ForgeMission.Rooms.Data;

/// <summary>
/// Write-side of the data-access seam — backed by the WriteConnection slot.
/// Messages are append-only: there is deliberately no update/delete surface.
/// </summary>
public interface IWriteStore
{
    Task<Member> AddMemberAsync(Member member, CancellationToken ct = default);

    /// <summary>Refresh mutable profile fields (display name / email) on an existing member.</summary>
    Task UpdateMemberProfileAsync(Guid memberId, string displayName, string? email, CancellationToken ct = default);

    Task<Room> AddRoomAsync(Room room, CancellationToken ct = default);

    Task<RoomInvite> AddInviteAsync(RoomInvite invite, CancellationToken ct = default);

    Task<RoomMembership> AddMembershipAsync(RoomMembership membership, CancellationToken ct = default);

    /// <summary>Remove a membership (e.g. take an agent back out of a room). Returns false if absent.</summary>
    Task<bool> RemoveMembershipAsync(Guid roomId, Guid memberId, CancellationToken ct = default);

    /// <summary>INSERT only — messages are immutable once appended.</summary>
    Task<Message> AppendMessageAsync(Message message, CancellationToken ct = default);
}
