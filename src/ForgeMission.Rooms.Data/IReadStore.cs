namespace ForgeMission.Rooms.Data;

/// <summary>
/// Read-side of the data-access seam. Call sites declaring read intent go through
/// this — backed by the ReadConnection slot, NoTracking, always paginated.
/// Never assume reads and writes share a connection (replica lag later).
/// </summary>
public interface IReadStore
{
    Task<Room?> GetRoomAsync(Guid roomId, CancellationToken ct = default);

    Task<Member?> GetMemberAsync(Guid memberId, CancellationToken ct = default);

    Task<IReadOnlyList<Member>> GetMembersAsync(MemberKind kind, CancellationToken ct = default);

    Task<IReadOnlyList<Room>> GetRoomsForMemberAsync(Guid memberId, CancellationToken ct = default);

    Task<IReadOnlyList<Member>> GetRoomMembersAsync(Guid roomId, CancellationToken ct = default);

    /// <summary>True iff a membership row exists — the confidentiality check.</summary>
    Task<bool> IsMemberAsync(Guid roomId, Guid memberId, CancellationToken ct = default);

    /// <summary>
    /// The most recent <paramref name="limit"/> messages before <paramref name="before"/>
    /// (or the newest, when null), returned in chronological order. Keyset pagination on
    /// (room_id, created_at) — the only way messages are ever read.
    /// </summary>
    Task<IReadOnlyList<Message>> GetRecentMessagesAsync(
        Guid roomId, int limit, DateTimeOffset? before = null, CancellationToken ct = default);
}
