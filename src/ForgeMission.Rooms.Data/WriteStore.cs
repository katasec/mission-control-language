using Microsoft.EntityFrameworkCore;

namespace ForgeMission.Rooms.Data;

public sealed class WriteStore(IDbContextFactory<RoomsDbContext> factory) : IWriteStore
{
    public async Task<Member> AddMemberAsync(Member member, CancellationToken ct = default)
    {
        if (member.Id == Guid.Empty) member.Id = Guid.NewGuid();
        if (member.CreatedAt == default) member.CreatedAt = DateTimeOffset.UtcNow;
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Members.Add(member);
        await db.SaveChangesAsync(ct);
        return member;
    }

    public async Task<Room> AddRoomAsync(Room room, CancellationToken ct = default)
    {
        if (room.Id == Guid.Empty) room.Id = Guid.NewGuid();
        if (room.CreatedAt == default) room.CreatedAt = DateTimeOffset.UtcNow;
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Rooms.Add(room);
        await db.SaveChangesAsync(ct);
        return room;
    }

    public async Task<RoomMembership> AddMembershipAsync(RoomMembership membership, CancellationToken ct = default)
    {
        if (membership.Id == Guid.Empty) membership.Id = Guid.NewGuid();
        if (membership.JoinedAt == default) membership.JoinedAt = DateTimeOffset.UtcNow;
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Memberships.Add(membership);
        await db.SaveChangesAsync(ct);
        return membership;
    }

    public async Task<Message> AppendMessageAsync(Message message, CancellationToken ct = default)
    {
        if (message.Id == Guid.Empty) message.Id = Guid.NewGuid();
        if (message.CreatedAt == default) message.CreatedAt = DateTimeOffset.UtcNow;
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Messages.Add(message);
        await db.SaveChangesAsync(ct);
        return message;
    }
}
