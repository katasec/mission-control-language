using ForgeMission.Rooms;
using ForgeMission.Rooms.Data;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeMission.Rooms.Tests;

/// <summary>
/// 38.4 store-level guarantees: federated-identity linkage, the membership confidentiality
/// boundary, roles, and invite acceptance. The provisioning/invite *services* live in the host,
/// but their load-bearing DB behaviour is exercised here against real Postgres.
/// </summary>
public sealed class IdentityAndInviteTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private IReadStore Reads => fixture.Services.GetRequiredService<IReadStore>();
    private IWriteStore Writes => fixture.Services.GetRequiredService<IWriteStore>();

    [Fact]
    public async Task Member_is_resolvable_by_issuer_and_subject()
    {
        var created = await Writes.AddMemberAsync(new Member
        {
            Kind = MemberKind.Human, DisplayName = "Dana", Issuer = "entra-external-id",
            Subject = "sub-dana", Email = "dana@example.com",
        });

        var found = await Reads.GetMemberBySubjectAsync("entra-external-id", "sub-dana");
        Assert.NotNull(found);
        Assert.Equal(created.Id, found!.Id);
        Assert.Null(await Reads.GetMemberBySubjectAsync("entra-external-id", "someone-else"));
    }

    [Fact]
    public async Task Duplicate_identity_is_rejected_by_the_unique_index()
    {
        await Writes.AddMemberAsync(new Member
        {
            Kind = MemberKind.Human, DisplayName = "One", Issuer = "dev", Subject = "dup-sub",
        });

        await Assert.ThrowsAnyAsync<Exception>(() => Writes.AddMemberAsync(new Member
        {
            Kind = MemberKind.Human, DisplayName = "Two", Issuer = "dev", Subject = "dup-sub",
        }));
    }

    [Fact]
    public async Task Agents_with_null_identity_do_not_collide_on_the_filtered_index()
    {
        // Two agents both have NULL (issuer, subject) — the filtered unique index must allow this.
        await Writes.AddMemberAsync(new Member { Kind = MemberKind.Agent, DisplayName = "@forge/a" });
        await Writes.AddMemberAsync(new Member { Kind = MemberKind.Agent, DisplayName = "@forge/b" });
    }

    [Fact]
    public async Task Membership_row_carries_the_room_role()
    {
        var (room, alice) = await ProvisionerRoomAsync("roles");

        var membership = await Reads.GetMembershipAsync(room.Id, alice.Id);
        Assert.NotNull(membership);
        Assert.Equal(MembershipRole.Provisioner, membership!.Role);
    }

    [Fact]
    public async Task Accepting_an_invite_adds_membership_with_the_granted_role()
    {
        var (room, creator) = await ProvisionerRoomAsync("invite");
        var invite = await Writes.AddInviteAsync(new RoomInvite
        {
            RoomId = room.Id, Token = "tok-consumer", Role = MembershipRole.Consumer, CreatedBy = creator.Id,
        });

        var newcomer = await Writes.AddMemberAsync(NewHuman("Newcomer", "newcomer"));
        Assert.Null(await Reads.GetMembershipAsync(room.Id, newcomer.Id)); // not a member yet

        // Simulate InviteService.AcceptAsync: resolve token, add membership with invite role.
        var loaded = await Reads.GetInviteByTokenAsync(invite.Token);
        Assert.NotNull(loaded);
        await Writes.AddMembershipAsync(new RoomMembership
        {
            RoomId = loaded!.RoomId, MemberId = newcomer.Id, Role = loaded.Role,
        });

        var membership = await Reads.GetMembershipAsync(room.Id, newcomer.Id);
        Assert.NotNull(membership);
        Assert.Equal(MembershipRole.Consumer, membership!.Role);
    }

    [Fact]
    public async Task Non_member_has_no_membership_row_and_sees_no_history()
    {
        var (room, alice) = await ProvisionerRoomAsync("confidential");
        await Writes.AppendMessageAsync(new Message
        {
            RoomId = room.Id, SenderId = alice.Id, SenderKind = MemberKind.Human, Kind = MessageKind.Human,
            Payload = new MessagePayload { Kind = MessagePayloadKinds.Human, Text = "secret" },
        });

        var outsider = await Writes.AddMemberAsync(NewHuman("Outsider", "outsider"));

        // The confidentiality gate: membership is null → the app denies before reading history.
        Assert.Null(await Reads.GetMembershipAsync(room.Id, outsider.Id));
        Assert.DoesNotContain(await Reads.GetRoomsForMemberAsync(outsider.Id), r => r.Id == room.Id);
    }

    private async Task<(Room room, Member provisioner)> ProvisionerRoomAsync(string name)
    {
        var room = await Writes.AddRoomAsync(new Room { Metadata = new RoomMetadata { Name = name } });
        var alice = await Writes.AddMemberAsync(NewHuman("Alice", $"alice-{name}"));
        await Writes.AddMembershipAsync(new RoomMembership
        {
            RoomId = room.Id, MemberId = alice.Id, Role = MembershipRole.Provisioner,
        });
        return (room, alice);
    }

    private static Member NewHuman(string name, string subject) => new()
    {
        Kind = MemberKind.Human, DisplayName = name, Issuer = "dev", Subject = subject, Email = $"{subject}@dev.local",
    };
}
