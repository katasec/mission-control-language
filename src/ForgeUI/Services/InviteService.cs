using System.Security.Cryptography;
using ForgeMission.Rooms;
using ForgeMission.Rooms.Data;

namespace ForgeUI.Services;

/// <summary>
/// Room invites (38.4) — the primary on-ramp. Creating an invite is a provisioner-only action;
/// accepting one adds the caller to the room (idempotently) with the invite's granted role.
/// Membership stays the DB-enforced confidentiality boundary; this only ever *adds* rows.
/// </summary>
public sealed class InviteService(IReadStore reads, IWriteStore writes)
{
    public async Task<RoomInvite> CreateAsync(
        Guid roomId, Member creator, MembershipRole grantedRole, TimeSpan? ttl, CancellationToken ct = default)
    {
        var membership = await reads.GetMembershipAsync(roomId, creator.Id, ct)
            ?? throw new UnauthorizedAccessException("Not a member of this room.");
        if (membership.Role != MembershipRole.Provisioner)
            throw new UnauthorizedAccessException("Only a provisioner can create invites.");

        return await writes.AddInviteAsync(new RoomInvite
        {
            RoomId = roomId,
            Token = GenerateToken(),
            Role = grantedRole,
            CreatedBy = creator.Id,
            ExpiresAt = ttl is { } t ? DateTimeOffset.UtcNow.Add(t) : null,
        }, ct);
    }

    public async Task<InviteResult> AcceptAsync(string token, Member member, CancellationToken ct = default)
    {
        var invite = await reads.GetInviteByTokenAsync(token, ct);
        if (invite is null)
            return InviteResult.NotFound();
        if (invite.IsExpired(DateTimeOffset.UtcNow))
            return InviteResult.Expired();

        var existing = await reads.GetMembershipAsync(invite.RoomId, member.Id, ct);
        if (existing is null)
            await writes.AddMembershipAsync(new RoomMembership
            {
                RoomId = invite.RoomId,
                MemberId = member.Id,
                Role = invite.Role,
            }, ct);

        return InviteResult.Joined(invite.RoomId);
    }

    private static string GenerateToken()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
}

public enum InviteStatus { Joined, NotFound, Expired }

public readonly record struct InviteResult(InviteStatus Status, Guid RoomId)
{
    public static InviteResult Joined(Guid roomId) => new(InviteStatus.Joined, roomId);
    public static InviteResult NotFound() => new(InviteStatus.NotFound, Guid.Empty);
    public static InviteResult Expired() => new(InviteStatus.Expired, Guid.Empty);
}
