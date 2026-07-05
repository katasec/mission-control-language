using Microsoft.EntityFrameworkCore;

namespace ForgeMission.Rooms.Data;

public sealed class ReadStore(IDbContextFactory<ReadRoomsDbContext> factory) : IReadStore
{
    public async Task<Room?> GetRoomAsync(Guid roomId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Rooms.AsNoTracking().SingleOrDefaultAsync(r => r.Id == roomId, ct);
    }

    public async Task<IReadOnlyList<Room>> GetRoomsForMemberAsync(Guid memberId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Memberships.AsNoTracking()
            .Where(ms => ms.MemberId == memberId)
            .Join(db.Rooms, ms => ms.RoomId, r => r.Id, (ms, r) => r)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Member>> GetRoomMembersAsync(Guid roomId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Memberships.AsNoTracking()
            .Where(ms => ms.RoomId == roomId)
            .Join(db.Members, ms => ms.MemberId, m => m.Id, (ms, m) => m)
            .OrderBy(m => m.DisplayName)
            .ToListAsync(ct);
    }

    public async Task<bool> IsMemberAsync(Guid roomId, Guid memberId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Memberships.AsNoTracking()
            .AnyAsync(ms => ms.RoomId == roomId && ms.MemberId == memberId, ct);
    }

    public async Task<IReadOnlyList<Message>> GetRecentMessagesAsync(
        Guid roomId, int limit, DateTimeOffset? before = null, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.Messages.AsNoTracking().Where(m => m.RoomId == roomId);
        if (before is not null)
            query = query.Where(m => m.CreatedAt < before);

        var page = await query
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Take(limit)
            .ToListAsync(ct);

        page.Reverse(); // newest page, oldest-first for display
        return page;
    }
}
