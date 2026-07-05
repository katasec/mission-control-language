using Microsoft.EntityFrameworkCore;

namespace ForgeMission.Rooms.Data;

/// <summary>
/// Development-only, idempotent seed data: two dev-stub humans (38.1 stub identity),
/// the built-in @forge/hallucination-guard agent member (38.2's first agent), a demo
/// room with all three, and a second room (Alice only) to exercise room isolation.
/// Fixed ids make re-runs no-ops.
/// </summary>
public static class RoomsSeeder
{
    public static readonly Guid AliceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid BobId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid HallucinationGuardId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid DemoRoomId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid AlicePrivateRoomId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public static async Task SeedAsync(IDbContextFactory<RoomsDbContext> factory, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        await EnsureMemberAsync(db, AliceId, MemberKind.Human, "Alice", ct);
        await EnsureMemberAsync(db, BobId, MemberKind.Human, "Bob", ct);
        await EnsureMemberAsync(db, HallucinationGuardId, MemberKind.Agent, "@forge/hallucination-guard", ct);

        await EnsureRoomAsync(db, DemoRoomId, "Demo Room", "Alice, Bob, and the hallucination guard", ct);
        await EnsureRoomAsync(db, AlicePrivateRoomId, "Alice's Room", "Membership isolation check — Alice only", ct);

        await EnsureMembershipAsync(db, DemoRoomId, AliceId, ct);
        await EnsureMembershipAsync(db, DemoRoomId, BobId, ct);
        await EnsureMembershipAsync(db, DemoRoomId, HallucinationGuardId, ct);
        await EnsureMembershipAsync(db, AlicePrivateRoomId, AliceId, ct);

        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureMemberAsync(
        RoomsDbContext db, Guid id, MemberKind kind, string displayName, CancellationToken ct)
    {
        if (!await db.Members.AnyAsync(m => m.Id == id, ct))
            db.Members.Add(new Member
            {
                Id = id,
                Kind = kind,
                DisplayName = displayName,
                CreatedAt = DateTimeOffset.UtcNow,
            });
    }

    private static async Task EnsureRoomAsync(
        RoomsDbContext db, Guid id, string name, string description, CancellationToken ct)
    {
        if (!await db.Rooms.AnyAsync(r => r.Id == id, ct))
            db.Rooms.Add(new Room
            {
                Id = id,
                Metadata = new RoomMetadata { Name = name, Description = description },
                CreatedAt = DateTimeOffset.UtcNow,
            });
    }

    private static async Task EnsureMembershipAsync(
        RoomsDbContext db, Guid roomId, Guid memberId, CancellationToken ct)
    {
        if (!await db.Memberships.AnyAsync(m => m.RoomId == roomId && m.MemberId == memberId, ct))
            db.Memberships.Add(new RoomMembership
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                MemberId = memberId,
                JoinedAt = DateTimeOffset.UtcNow,
            });
    }
}
