using ForgeMission.Rooms;
using ForgeMission.Rooms.Data;

namespace ForgeUI.Services;

/// <summary>
/// User-initiated room creation (the "+ New room" journey): unlike
/// <see cref="StarterRoomService"/> — which auto-provisions a single onboarding room — this lets a
/// signed-in human name a room and pick which registry agents join it up front. The creator is the
/// provisioner; each chosen @handle is added as a consumer member (same membership seam the
/// add-agent panel uses), so the room is usable the moment it opens.
/// </summary>
public sealed class RoomCreationService(IReadStore reads, IWriteStore writes, AgentRegistry agents)
{
    /// <summary>The agents a creator may drop into a new room — the whole shared registry today.</summary>
    public IReadOnlyList<AgentDescriptor> AvailableAgents() => agents.List();

    /// <summary>
    /// Create a room named <paramref name="name"/> owned by <paramref name="human"/>, seeding the
    /// requested agent handles as members. Unknown/unbound handles are skipped (never green-lit),
    /// mirroring <see cref="RoomAgentMembershipService.AddAgentAsync"/>. Returns the new room id.
    /// </summary>
    public async Task<Guid> CreateAsync(
        Member human, string name, IEnumerable<string> agentHandles, CancellationToken ct = default)
    {
        var trimmed = name.Trim();
        var room = await writes.AddRoomAsync(new Room
        {
            Metadata = new RoomMetadata
            {
                Name = string.IsNullOrEmpty(trimmed) ? "New room" : trimmed,
            },
            CreatedAt = DateTimeOffset.UtcNow,
        }, ct);

        // The creator provisions their own space.
        await writes.AddMembershipAsync(new RoomMembership
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            MemberId = human.Id,
            Role = MembershipRole.Provisioner,
            JoinedAt = DateTimeOffset.UtcNow,
        }, ct);

        // Resolve chosen handles to their seeded agent-member identities and add each as a consumer.
        var agentMembers = await reads.GetMembersAsync(MemberKind.Agent, ct);
        foreach (var handle in agentHandles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!agents.TryResolveDescriptor(handle, out _))
                continue; // unknown / unbound handle — skip, never add a phantom member

            var agent = agentMembers.FirstOrDefault(m =>
                string.Equals(m.DisplayName, handle, StringComparison.OrdinalIgnoreCase));
            if (agent is null)
                continue;

            await writes.AddMembershipAsync(new RoomMembership
            {
                Id = Guid.NewGuid(),
                RoomId = room.Id,
                MemberId = agent.Id,
                Role = MembershipRole.Consumer,
                JoinedAt = DateTimeOffset.UtcNow,
            }, ct);
        }

        return room.Id;
    }
}
