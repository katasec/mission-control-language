using ForgeMission.Rooms;
using ForgeMission.Rooms.Data;

namespace ForgeUI.Services;

/// <summary>
/// Provisioner-only room administration that isn't agent membership (which lives in
/// <see cref="RoomAgentMembershipService"/>) — today just renaming. Gated on the same provisioner
/// role check so a consumer can never mutate a room's identity.
/// </summary>
public sealed class RoomAdminService(IReadStore reads, IWriteStore writes)
{
    /// <summary>Rename the room. Provisioner-gated; no-ops on a blank name.</summary>
    public async Task<bool> RenameAsync(Guid roomId, Member actor, string name, CancellationToken ct = default)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
            return false;

        var membership = await reads.GetMembershipAsync(roomId, actor.Id, ct);
        if (membership?.Role != MembershipRole.Provisioner)
            throw new UnauthorizedAccessException("Only a room provisioner can rename the room.");

        return await writes.RenameRoomAsync(roomId, trimmed, ct);
    }
}
