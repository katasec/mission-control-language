using ForgeMission.Rooms;
using ForgeMission.Rooms.Data;
using ForgeUI.Models;
using ForgeUI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ForgeUI.Hubs;

/// <summary>
/// SignalR delivery for <b>external</b> clients (future native/mobile) — the Blazor app talks
/// to <see cref="RoomBroadcaster"/> in-process instead. Authorized (38.4): the connection must
/// be authenticated, and the acting member is derived from the connection's principal
/// (<see cref="HubCallerContext.User"/>) — never a client-supplied id. Membership is re-checked
/// server-side on join and (inside <see cref="RoomMessageService"/>) on send.
/// </summary>
[Authorize]
public sealed class ChatHub(
    IReadStore reads,
    MemberProvisioningService provisioning,
    RoomMessageService messages) : Hub
{
    public async Task JoinRoom(Guid roomId)
    {
        var member = await CurrentMemberAsync();
        if (await reads.GetMembershipAsync(roomId, member.Id) is null)
            throw new HubException("Not a member of this room.");
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(roomId));
    }

    public Task LeaveRoom(Guid roomId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(roomId));

    public async Task SendMessage(Guid roomId, string text)
    {
        var member = await CurrentMemberAsync();
        await messages.SendHumanMessageAsync(roomId, member, text);
    }

    private async Task<Member> CurrentMemberAsync()
        => await provisioning.ResolveAsync(Context.User)
           ?? throw new HubException("Unknown or unprovisioned member.");

    public static string GroupName(Guid roomId) => $"room:{roomId}";
}
