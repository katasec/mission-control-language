using Microsoft.EntityFrameworkCore;

namespace ForgeMission.Rooms.Data;

/// <summary>
/// Development-only, idempotent seed data: two humans with dev federated identities
/// (issuer "dev"), the built-in @forge/hallucination-guard agent member, a demo room with
/// all three (Alice provisioner, Bob consumer), and a second room (Alice only) for the
/// isolation check. Fixed ids make re-runs no-ops. Dev sign-in maps to these members by
/// (issuer, subject) through the same provisioning path real OIDC uses.
/// </summary>
public static class RoomsSeeder
{
    public const string DevIssuer = "dev";

    public static readonly Guid AliceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid BobId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid HallucinationGuardId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    // The default general assistant dropped into every new user's starter room (LLM-verified).
    public static readonly Guid AssistantId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public const string AssistantHandle = "@forge/assistant";
    public static readonly Guid DemoRoomId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid AlicePrivateRoomId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    /// <summary>
    /// Essential PRODUCT data seeded in EVERY environment (idempotent): the built-in agent
    /// members that starter rooms reference. Unlike <see cref="SeedAsync"/> (dev test humans +
    /// demo rooms), these must exist in prod too, or <c>StarterRoomService</c> hits an FK
    /// violation adding the assistant to a new user's room.
    /// </summary>
    public static async Task SeedEssentialAgentsAsync(IDbContextFactory<RoomsDbContext> factory, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await EnsureAgentAsync(db, HallucinationGuardId, "@forge/hallucination-guard", ct);
        await EnsureAgentAsync(db, AssistantId, AssistantHandle, ct);
        await db.SaveChangesAsync(ct);
    }

    public static async Task SeedAsync(IDbContextFactory<RoomsDbContext> factory, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        await EnsureHumanAsync(db, AliceId, "Alice", "alice", "alice@dev.local", ct);
        await EnsureHumanAsync(db, BobId, "Bob", "bob", "bob@dev.local", ct);
        await EnsureAgentAsync(db, HallucinationGuardId, "@forge/hallucination-guard", ct);
        await EnsureAgentAsync(db, AssistantId, AssistantHandle, ct);

        await EnsureRoomAsync(db, DemoRoomId, "Demo Room", "Alice, Bob, and the hallucination guard", ct);
        await EnsureRoomAsync(db, AlicePrivateRoomId, "Alice's Room", "Membership isolation check — Alice only", ct);

        // Alice provisions the demo room; Bob is a consumer.
        await EnsureMembershipAsync(db, DemoRoomId, AliceId, MembershipRole.Provisioner, ct);
        await EnsureMembershipAsync(db, DemoRoomId, BobId, MembershipRole.Consumer, ct);
        await EnsureMembershipAsync(db, DemoRoomId, HallucinationGuardId, MembershipRole.Consumer, ct);
        await EnsureMembershipAsync(db, AlicePrivateRoomId, AliceId, MembershipRole.Provisioner, ct);

        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureHumanAsync(
        RoomsDbContext db, Guid id, string displayName, string subject, string email, CancellationToken ct)
    {
        if (!await db.Members.AnyAsync(m => m.Id == id, ct))
            db.Members.Add(new Member
            {
                Id = id,
                Kind = MemberKind.Human,
                DisplayName = displayName,
                Issuer = DevIssuer,
                Subject = subject,
                Email = email,
                CreatedAt = DateTimeOffset.UtcNow,
            });
    }

    private static async Task EnsureAgentAsync(
        RoomsDbContext db, Guid id, string displayName, CancellationToken ct)
    {
        if (!await db.Members.AnyAsync(m => m.Id == id, ct))
            db.Members.Add(new Member
            {
                Id = id,
                Kind = MemberKind.Agent,
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
        RoomsDbContext db, Guid roomId, Guid memberId, MembershipRole role, CancellationToken ct)
    {
        if (!await db.Memberships.AnyAsync(m => m.RoomId == roomId && m.MemberId == memberId, ct))
            db.Memberships.Add(new RoomMembership
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                MemberId = memberId,
                Role = role,
                JoinedAt = DateTimeOffset.UtcNow,
            });
    }
}
