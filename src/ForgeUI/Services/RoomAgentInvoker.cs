using ForgeMission.Rooms;
using ForgeMission.Rooms.Data;
using ForgeUI.Hubs;
using ForgeUI.Models;
using Microsoft.AspNetCore.SignalR;

namespace ForgeUI.Services;

/// <summary>
/// Bridges a room @mention to a mission run and streams the result back into the room (38.2
/// tasks 4+5). Runs the mission in the background so the sender's hub call returns at once;
/// each invocation is independent (Q2 — concurrent runs are fine). Broadcasts via
/// <see cref="IHubContext{ChatHub}"/> (not a hub instance, which is per-call and gone once the
/// send returns). Pull-only: only ever called when an agent member was addressed.
/// </summary>
public sealed class RoomAgentInvoker(
    IHubContext<ChatHub> hub,
    IReadStore reads,
    IWriteStore writes,
    MissionRegistry registry,
    AgentCatalog catalog,
    RoomContextAssembler assembler,
    ILogger<RoomAgentInvoker> logger)
{
    /// <summary>Hard ceiling on a single agent run (retries included).</summary>
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(3);

    /// <summary>Fire-and-forget: the mission runs off the caller's hub invocation.</summary>
    public void Invoke(Guid roomId, Member agent, string handle, string prompt, Guid? replyTo, Guid triggerMessageId)
        => _ = Task.Run(() => RunAsync(roomId, agent, handle, prompt, replyTo, triggerMessageId));

    private async Task RunAsync(Guid roomId, Member agent, string handle, string prompt, Guid? replyTo, Guid triggerMessageId)
    {
        var group = ChatHub.GroupName(roomId);
        using var cts = new CancellationTokenSource(RunTimeout);
        var ct = cts.Token;

        try
        {
            if (!catalog.TryResolve(handle, out var mission))
            {
                logger.LogWarning("No mission bound to {Handle} (room {RoomId})", handle, roomId);
                await PostAsync(roomId, group, agent, handle, triggerMessageId,
                    $"No mission is bound to {handle} yet.", verified: false, stepCount: 0, retryCount: 0, trace: [], ct);
                return;
            }

            await hub.Clients.Group(group).SendAsync("AgentThinking", roomId, agent.Id, handle, ct);

            var memberNames = (await reads.GetRoomMembersAsync(roomId, ct)).ToDictionary(m => m.Id, m => m.DisplayName);
            var goal = await assembler.BuildGoalAsync(roomId, prompt, replyTo, triggerMessageId, memberNames, ct);

            logger.LogInformation("Agent {Handle} running in room {RoomId}", handle, roomId);

            // Reuse the Phase 35 engine bridge untouched.
            var result = await new MissionService(registry).RunAsync(goal, mission, onStep: _ => { }, ct);

            var trace = result.Trace
                .Select(t => new AgentStep
                {
                    ExpertName = t.ExpertName,
                    Status = t.Envelope.Status,
                    Text = t.Envelope.Text,
                    Attempt = t.Attempt,
                })
                .ToList();

            await PostAsync(roomId, group, agent, handle, triggerMessageId,
                result.AgentText ?? "(no answer)",
                verified: result.Trust?.Verified ?? false,
                stepCount: result.Trust?.StepCount ?? trace.Count,
                retryCount: result.Trust?.RetryCount ?? 0,
                trace: trace, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Agent {Handle} failed in room {RoomId}", handle, roomId);
            try
            {
                await hub.Clients.Group(group).SendAsync("AgentFailed", roomId, agent.Id, handle, CancellationToken.None);
            }
            catch (Exception broadcastEx)
            {
                logger.LogError(broadcastEx, "Failed to broadcast AgentFailed for {Handle}", handle);
            }
        }
    }

    private async Task PostAsync(
        Guid roomId, string group, Member agent, string handle, Guid triggerMessageId,
        string text, bool verified, int stepCount, int retryCount, List<AgentStep> trace, CancellationToken ct)
    {
        var message = await writes.AppendMessageAsync(new Message
        {
            RoomId = roomId,
            SenderId = agent.Id,
            SenderKind = MemberKind.Agent,
            Kind = MessageKind.Agent,
            ReplyTo = triggerMessageId,
            Payload = new MessagePayload
            {
                Kind = MessagePayloadKinds.Agent,
                Text = text,
                Agent = new AgentMeta
                {
                    Handle = handle,
                    Verified = verified,
                    StepCount = stepCount,
                    RetryCount = retryCount,
                    Trace = trace,
                },
            },
        }, ct);

        await hub.Clients.Group(group).SendAsync("ReceiveMessage", message.ToDto(agent.DisplayName), ct);
    }
}
