using ForgeMission.Rooms;
using ForgeMission.Rooms.Data;

namespace ForgeUI.Services;

/// <summary>
/// The "add / remove @agent" journey (38.5 task 3): a provisioner pulls an agent from the
/// registry (GAL) into a room as a member, or takes it back out. Membership is the
/// confidentiality boundary and the addressing gate — an agent can only be @-addressed in a
/// room it is a member of (that is why <c>@openai</c> from <c>/agents</c> isn't reachable until
/// it's added here). Built-in agents exist as seeded member identities; adding just creates the
/// room membership.
/// </summary>
public sealed class RoomAgentMembershipService(IReadStore reads, IWriteStore writes, AgentRegistry agents)
{
    /// <summary>Registry agents not already members of the room — the "addable" set for the picker.</summary>
    public async Task<IReadOnlyList<AgentDescriptor>> AddableAgentsAsync(Guid roomId, CancellationToken ct = default)
    {
        var present = (await reads.GetRoomMembersAsync(roomId, ct))
            .Where(m => m.Kind == MemberKind.Agent)
            .Select(m => m.DisplayName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return agents.List().Where(d => !present.Contains(d.Handle)).ToList();
    }

    /// <summary>
    /// Add the agent behind <paramref name="handle"/> to the room as a consumer member.
    /// Provisioner-gated; idempotent. Returns false if the handle isn't a known bound agent.
    /// </summary>
    public async Task<bool> AddAgentAsync(Guid roomId, Member actor, string handle, CancellationToken ct = default)
    {
        await EnsureProvisionerAsync(roomId, actor, ct);

        // Only agents that are actually in the registry (bound to a loaded mission) can be added —
        // never green-lights an unbound handle.
        if (!agents.TryResolveDescriptor(handle, out _))
            return false;

        var agent = (await reads.GetMembersAsync(MemberKind.Agent, ct))
            .FirstOrDefault(m => string.Equals(m.DisplayName, handle, StringComparison.OrdinalIgnoreCase));
        if (agent is null)
            return false;

        if (await reads.GetMembershipAsync(roomId, agent.Id, ct) is not null)
            return true; // already a member

        await writes.AddMembershipAsync(new RoomMembership
        {
            RoomId = roomId,
            MemberId = agent.Id,
            Role = MembershipRole.Consumer,
            JoinedAt = DateTimeOffset.UtcNow,
        }, ct);
        return true;
    }

    /// <summary>Remove an agent member from the room. Provisioner-gated.</summary>
    public async Task<bool> RemoveAgentAsync(Guid roomId, Member actor, Guid agentMemberId, CancellationToken ct = default)
    {
        await EnsureProvisionerAsync(roomId, actor, ct);
        return await writes.RemoveMembershipAsync(roomId, agentMemberId, ct);
    }

    private async Task EnsureProvisionerAsync(Guid roomId, Member actor, CancellationToken ct)
    {
        var membership = await reads.GetMembershipAsync(roomId, actor.Id, ct);
        if (membership?.Role != MembershipRole.Provisioner)
            throw new UnauthorizedAccessException("Only a room provisioner can manage agents.");
    }
}
