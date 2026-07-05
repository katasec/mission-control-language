using ForgeMission.Rooms;
using ForgeMission.Rooms.Data;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeMission.Rooms.Tests;

public sealed class RoomStoreTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private IReadStore Reads => fixture.Services.GetRequiredService<IReadStore>();
    private IWriteStore Writes => fixture.Services.GetRequiredService<IWriteStore>();

    [Fact]
    public async Task Append_and_read_roundtrips_with_jsonb_payload()
    {
        var (room, alice, _) = await SeedRoomAsync("roundtrip");

        await Writes.AppendMessageAsync(NewHumanMessage(room.Id, alice, "hello world"));

        var history = await Reads.GetRecentMessagesAsync(room.Id, limit: 50);
        var msg = Assert.Single(history);
        Assert.Equal("hello world", msg.Payload.Text);
        Assert.Equal(1, msg.Payload.V);                                  // discriminator survives jsonb
        Assert.Equal(MessagePayloadKinds.Human, msg.Payload.Kind);
        Assert.Equal(alice.Id, msg.SenderId);
        Assert.Equal(MemberKind.Human, msg.SenderKind);
    }

    [Fact]
    public async Task Recent_messages_are_paginated_oldest_first_within_the_page()
    {
        var (room, alice, _) = await SeedRoomAsync("pagination");

        for (var i = 0; i < 10; i++)
        {
            await Writes.AppendMessageAsync(NewHumanMessage(room.Id, alice, $"msg {i}"));
            await Task.Delay(2); // distinct created_at ordering
        }

        var page = await Reads.GetRecentMessagesAsync(room.Id, limit: 3);

        // The 3 most recent, returned oldest-first for display.
        Assert.Equal(3, page.Count);
        Assert.Equal(["msg 7", "msg 8", "msg 9"], page.Select(m => m.Payload.Text));
    }

    [Fact]
    public async Task Messages_are_isolated_by_room()
    {
        var (roomA, alice, _) = await SeedRoomAsync("iso-a");
        var (roomB, bob, _) = await SeedRoomAsync("iso-b");

        await Writes.AppendMessageAsync(NewHumanMessage(roomA.Id, alice, "in A"));
        await Writes.AppendMessageAsync(NewHumanMessage(roomB.Id, bob, "in B"));

        var a = await Reads.GetRecentMessagesAsync(roomA.Id, limit: 50);
        var b = await Reads.GetRecentMessagesAsync(roomB.Id, limit: 50);

        Assert.Equal("in A", Assert.Single(a).Payload.Text);
        Assert.Equal("in B", Assert.Single(b).Payload.Text);
    }

    [Fact]
    public async Task Membership_is_the_confidentiality_boundary()
    {
        var (room, alice, _) = await SeedRoomAsync("membership");
        var outsider = await Writes.AddMemberAsync(NewHuman("Outsider"));

        Assert.True(await Reads.IsMemberAsync(room.Id, alice.Id));
        Assert.False(await Reads.IsMemberAsync(room.Id, outsider.Id));

        var aliceRooms = await Reads.GetRoomsForMemberAsync(alice.Id);
        Assert.Contains(aliceRooms, r => r.Id == room.Id);

        var outsiderRooms = await Reads.GetRoomsForMemberAsync(outsider.Id);
        Assert.DoesNotContain(outsiderRooms, r => r.Id == room.Id);
    }

    [Fact]
    public async Task Duplicate_membership_is_rejected_by_the_unique_constraint()
    {
        var (room, alice, _) = await SeedRoomAsync("dup");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            Writes.AddMembershipAsync(new RoomMembership { RoomId = room.Id, MemberId = alice.Id }));
    }

    [Fact]
    public async Task Room_metadata_roundtrips_through_jsonb()
    {
        var room = await Writes.AddRoomAsync(new Room
        {
            Metadata = new RoomMetadata { Name = "Named Room", Description = "desc" },
        });

        var loaded = await Reads.GetRoomAsync(room.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Named Room", loaded!.Metadata.Name);
        Assert.Equal("desc", loaded.Metadata.Description);
        Assert.Equal(1, loaded.Metadata.V);
    }

    private async Task<(Room room, Member alice, Member bob)> SeedRoomAsync(string name)
    {
        var room = await Writes.AddRoomAsync(new Room { Metadata = new RoomMetadata { Name = name } });
        var alice = await Writes.AddMemberAsync(NewHuman("Alice"));
        var bob = await Writes.AddMemberAsync(NewHuman("Bob"));
        await Writes.AddMembershipAsync(new RoomMembership { RoomId = room.Id, MemberId = alice.Id });
        await Writes.AddMembershipAsync(new RoomMembership { RoomId = room.Id, MemberId = bob.Id });
        return (room, alice, bob);
    }

    private static Member NewHuman(string name) => new()
    {
        Kind = MemberKind.Human,
        DisplayName = name,
    };

    private static Message NewHumanMessage(Guid roomId, Member sender, string text) => new()
    {
        RoomId = roomId,
        SenderId = sender.Id,
        SenderKind = sender.Kind,
        Kind = MessageKind.Human,
        Payload = new MessagePayload { Kind = MessagePayloadKinds.Human, Text = text },
    };
}
