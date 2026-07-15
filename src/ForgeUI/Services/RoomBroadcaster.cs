using System.Collections.Concurrent;
using ForgeUI.Hubs;
using ForgeUI.Models;
using Microsoft.AspNetCore.SignalR;

namespace ForgeUI.Services;

/// <summary>
/// Room delivery fan-out (38.4). The Blazor client runs *inside* an already-authenticated
/// server circuit, so it subscribes here in-process — it never opens a second SignalR
/// connection to <see cref="ChatHub"/> (which couldn't carry its cookie). External SignalR
/// clients (future: native/mobile) still get every event via <see cref="IHubContext{ChatHub}"/>,
/// which the hub authorizes independently. One publish, both audiences.
/// </summary>
public sealed class RoomBroadcaster(IHubContext<ChatHub> hub)
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Func<RoomEvent, Task>>> _subscribers = new();

    public IDisposable Subscribe(Guid roomId, Func<RoomEvent, Task> handler)
    {
        var forRoom = _subscribers.GetOrAdd(roomId, _ => new());
        var id = Guid.NewGuid();
        forRoom[id] = handler;
        return new Subscription(() =>
        {
            if (_subscribers.TryGetValue(roomId, out var map))
                map.TryRemove(id, out _);
        });
    }

    public Task PublishMessageAsync(Guid roomId, RoomMessageDto message)
        => PublishAsync(roomId, new MessagePosted(message), "ReceiveMessage", message);

    public Task PublishAgentThinkingAsync(Guid roomId, Guid agentId, string handle)
        => PublishAsync(roomId, new AgentThinking(agentId, handle), "AgentThinking", roomId, agentId, handle);

    /// <summary>Transient step-progress on the pending agent bubble (41.7): a live label like
    /// "Searching the web…" that replaces the frozen spinner. Not persisted — only the final message is.</summary>
    public Task PublishAgentProgressAsync(Guid roomId, Guid agentId, string handle, string label)
        => PublishAsync(roomId, new AgentProgress(agentId, handle, label), "AgentProgress", roomId, agentId, handle, label);

    public Task PublishAgentFailedAsync(Guid roomId, Guid agentId, string handle)
        => PublishAsync(roomId, new AgentFailed(agentId, handle), "AgentFailed", roomId, agentId, handle);

    private async Task PublishAsync(Guid roomId, RoomEvent evt, string hubMethod, params object[] hubArgs)
    {
        // In-process Blazor subscribers.
        if (_subscribers.TryGetValue(roomId, out var handlers))
        {
            foreach (var handler in handlers.Values)
            {
                try { await handler(evt); }
                catch { /* one subscriber's failure must not block the others */ }
            }
        }

        // External SignalR clients (authorized by the hub on their own connection).
        await hub.Clients.Group(ChatHub.GroupName(roomId)).SendAsync(hubMethod, hubArgs);
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}

/// <summary>Room delivery events for in-process subscribers.</summary>
public abstract record RoomEvent;
public sealed record MessagePosted(RoomMessageDto Message) : RoomEvent;
public sealed record AgentThinking(Guid AgentId, string Handle) : RoomEvent;
public sealed record AgentProgress(Guid AgentId, string Handle, string Label) : RoomEvent;
public sealed record AgentFailed(Guid AgentId, string Handle) : RoomEvent;
