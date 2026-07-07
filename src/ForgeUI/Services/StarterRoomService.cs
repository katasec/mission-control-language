using ForgeMission.Rooms;
using ForgeMission.Rooms.Data;

namespace ForgeUI.Services;

/// <summary>
/// Onboarding: a brand-new user should land in a usable chat, not an empty rooms list. On first
/// sight (zero rooms) we create a private "room of two" — the user as provisioner plus the
/// built-in <see cref="RoomsSeeder.AssistantHandle"/> agent — so they can start talking to a
/// verified assistant immediately and invite people later. Idempotent by "has any room".
/// </summary>
public sealed class StarterRoomService(IReadStore reads, IWriteStore writes)
{
    /// <summary>
    /// Ensures <paramref name="human"/> has at least one room. Returns the new starter room's id
    /// when one was created, or null if the user already belongs to a room.
    /// </summary>
    public async Task<Guid?> EnsureStarterRoomAsync(Member human, CancellationToken ct = default)
    {
        var rooms = await reads.GetRoomsForMemberAsync(human.Id, ct);
        if (rooms.Count > 0)
        {
            // Self-heal solo "room of two" spaces created before the assistant member existed
            // (e.g. rooms provisioned in prod before SeedEssentialAgentsAsync): if the user is
            // alone with no agent, add the assistant so the auto-reply works.
            foreach (var existing in rooms)
            {
                var members = await reads.GetRoomMembersAsync(existing.Id, ct);
                var soloHuman = members.Count(m => m.Kind == MemberKind.Human) == 1;
                var hasAgent = members.Any(m => m.Kind == MemberKind.Agent);
                if (soloHuman && !hasAgent)
                    await writes.AddMembershipAsync(new RoomMembership
                    {
                        Id = Guid.NewGuid(),
                        RoomId = existing.Id,
                        MemberId = RoomsSeeder.AssistantId,
                        Role = MembershipRole.Consumer,
                        JoinedAt = DateTimeOffset.UtcNow,
                    }, ct);
            }
            return null;
        }

        var room = await writes.AddRoomAsync(new Room
        {
            Metadata = new RoomMetadata
            {
                Name = "Getting started",
                Description = $"Your private space — chat with {RoomsSeeder.AssistantHandle}, invite people when you're ready.",
            },
            CreatedAt = DateTimeOffset.UtcNow,
        }, ct);

        // The user provisions their own space; the assistant is a member (a "room of two").
        await writes.AddMembershipAsync(new RoomMembership
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            MemberId = human.Id,
            Role = MembershipRole.Provisioner,
            JoinedAt = DateTimeOffset.UtcNow,
        }, ct);

        await writes.AddMembershipAsync(new RoomMembership
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            MemberId = RoomsSeeder.AssistantId,
            Role = MembershipRole.Consumer,
            JoinedAt = DateTimeOffset.UtcNow,
        }, ct);

        return room.Id;
    }
}
