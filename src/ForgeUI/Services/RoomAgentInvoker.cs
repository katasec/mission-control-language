using ForgeMission.Rooms;
using ForgeMission.Rooms.Data;
using ForgeMission.Runner.Contracts;
using ForgeUI.Models; // ToDto extension (RoomsMappings)

namespace ForgeUI.Services;

/// <summary>
/// Bridges a room @mention to a mission run and streams the result back into the room (38.2
/// tasks 4+5). Runs the mission in the background so the sender's hub call returns at once;
/// each invocation is independent (Q2 — concurrent runs are fine). Broadcasts via
/// <see cref="IHubContext{ChatHub}"/> (not a hub instance, which is per-call and gone once the
/// send returns). Pull-only: only ever called when an agent member was addressed.
/// <para>
/// Since Phase 39.1 the run itself happens in the containerised runner (<see cref="MissionRunnerClient"/>),
/// not in-process — the orchestrator assembles context, invokes the runner, and persists/broadcasts
/// the result. The user-visible surface is unchanged.
/// </para>
/// </summary>
public sealed class RoomAgentInvoker(
    RoomBroadcaster broadcaster,
    IReadStore reads,
    IWriteStore writes,
    MissionRunnerClient runner,
    AgentRegistry agents,
    RoomContextAssembler assembler,
    BillingService billing,
    ILogger<RoomAgentInvoker> logger)
{
    /// <summary>Hard ceiling on a single agent run (retries included).</summary>
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(3);

    /// <summary>Fire-and-forget: the mission runs off the caller's hub invocation.
    /// <paramref name="senderId"/> is the invoking human — the run's cost is debited to them (39.2).</summary>
    public void Invoke(Guid roomId, Guid senderId, Member agent, string handle, string prompt, Guid? replyTo, Guid triggerMessageId)
        => _ = Task.Run(() => RunAsync(roomId, senderId, agent, handle, prompt, replyTo, triggerMessageId));

    private async Task RunAsync(Guid roomId, Guid senderId, Member agent, string handle, string prompt, Guid? replyTo, Guid triggerMessageId)
    {
        using var cts = new CancellationTokenSource(RunTimeout);
        var ct = cts.Token;

        try
        {
            // The @handle directory binds to a mission ref (38.5 task 7). VerifiesAnswers gates the
            // green check: a raw-model passthrough must never be checked even though its no-judge
            // pipeline reports Pass. The neutral "not verified" chip is derived at render from the
            // same descriptor flag.
            if (!agents.TryResolveDescriptor(handle, out var descriptor))
            {
                logger.LogWarning("No mission bound to {Handle} (room {RoomId})", handle, roomId);
                await PostAsync(roomId, agent, handle, triggerMessageId,
                    $"No mission is bound to {handle} yet.", verified: false, stepCount: 0, retryCount: 0, trace: [], ct);
                return;
            }
            var verifies = descriptor.VerifiesAnswers;

            // Balance = cap = meter (39.2): a run is allowed while the invoker's balance is positive.
            // Check before showing "thinking" so an out-of-credits user gets a clear reply, not a
            // spinner that fails. Settlement (debit) happens after the run below.
            if (!await billing.HasCreditAsync(senderId, ct))
            {
                logger.LogInformation("Member {MemberId} out of credits — blocking {Handle}", senderId, handle);
                await PostAsync(roomId, agent, handle, triggerMessageId,
                    "You're out of credits. Top up to keep running agents.",
                    verified: false, stepCount: 0, retryCount: 0, trace: [], ct);
                return;
            }

            await broadcaster.PublishAgentThinkingAsync(roomId, agent.Id, handle);

            var memberNames = (await reads.GetRoomMembersAsync(roomId, ct)).ToDictionary(m => m.Id, m => m.DisplayName);
            var goal = await assembler.BuildGoalAsync(roomId, prompt, replyTo, triggerMessageId, memberNames, ct);

            logger.LogInformation("Agent {Handle} running in room {RoomId}", handle, roomId);

            // Run in the containerised runner (39.1), streaming progress (41.7): each step-start
            // becomes a transient "Searching the web…" chip on the pending bubble, so a 40–60s search
            // shows life instead of a frozen spinner. All built-ins run under the trusted policy;
            // custom missions get the restricted policy in 39.5.
            var result = await runner.RunStreamAsync(
                descriptor.MissionRef, goal, RunPolicy.Trusted,
                onProgress: p => broadcaster.PublishAgentProgressAsync(roomId, agent.Id, handle, ProgressLabel(p)),
                ct);

            var trace = result.Trace
                .Select(t => new AgentStep
                {
                    ExpertName = t.ExpertName,
                    Status = t.Status,
                    Text = t.Text,
                    Reason = t.Reason,
                    Attempt = t.Attempt,
                })
                .ToList();

            await PostAsync(roomId, agent, handle, triggerMessageId,
                string.IsNullOrEmpty(result.AgentText) ? "(no answer)" : result.AgentText,
                verified: verifies && result.Verified,
                stepCount: result.StepCount,
                retryCount: result.RetryCount,
                trace: trace, ct);

            // Settle: debit the run's actual cost after it completes (39.2). Charged even when the
            // answer didn't verify — the provider tokens were still spent (cost-recovery). Best-effort:
            // a ledger hiccup must not fail an already-delivered answer.
            try
            {
                await billing.SettleRunAsync(senderId, descriptor.MissionRef, result.Usage, ct);
            }
            catch (Exception settleEx)
            {
                logger.LogError(settleEx, "Failed to settle run cost for member {MemberId} ({Handle})", senderId, handle);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Agent {Handle} failed in room {RoomId}", handle, roomId);
            try
            {
                await broadcaster.PublishAgentFailedAsync(roomId, agent.Id, handle);
            }
            catch (Exception broadcastEx)
            {
                logger.LogError(broadcastEx, "Failed to broadcast AgentFailed for {Handle}", handle);
            }
        }
    }

    // Map an engine step kind to a human progress label. Provider-agnostic — the runner emits neutral
    // kinds (41.7) and the label lives here so every backend and mission shares it. Unknown kinds fall
    // back to a generic "Working…" rather than leaking an internal name.
    private static string ProgressLabel(RunProgress p) => p.Kind switch
    {
        "search"       => "Searching the web…",
        "llm"          => "Thinking…",
        "json_extract" => "Routing…",
        "http"         => "Fetching…",
        "exec"         => "Running…",
        "rule"         => "Checking…",
        "onnx"         => "Classifying…",
        _              => "Working…",
    };

    private async Task PostAsync(
        Guid roomId, Member agent, string handle, Guid triggerMessageId,
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

        await broadcaster.PublishMessageAsync(roomId, message.ToDto(agent.DisplayName));
    }
}
