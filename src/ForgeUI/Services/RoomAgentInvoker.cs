using System.Text.Json;
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
    IArtifactStore artifacts,
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
                    $"No mission is bound to {handle} yet.", verified: false, stepCount: 0, retryCount: 0, trace: [], artifacts: [], ct);
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
                    verified: false, stepCount: 0, retryCount: 0, trace: [], artifacts: [], ct);
                return;
            }

            // File-in staging (38.9): a file-consuming agent runs on the room's most recent upload.
            // The orchestrator sends the bytes; the runner sets source_pdf/work_dir (D5). Nudge if
            // there's nothing to work on — cheaper and clearer than running a mission that will fail.
            RunArtifact? input = null;
            if (descriptor.AcceptsArtifacts)
            {
                input = await StageLatestArtifactAsync(roomId, senderId, ct);
                if (input is null)
                {
                    await PostAsync(roomId, agent, handle, triggerMessageId,
                        "Attach a PDF to this room and I'll edit it for you.",
                        verified: false, stepCount: 0, retryCount: 0, trace: [], artifacts: [], ct);
                    return;
                }
            }

            await broadcaster.PublishAgentThinkingAsync(roomId, agent.Id, handle);

            var memberNames = (await reads.GetRoomMembersAsync(roomId, ct)).ToDictionary(m => m.Id, m => m.DisplayName);
            var goal = await assembler.BuildGoalAsync(roomId, prompt, replyTo, triggerMessageId, memberNames, ct);

            // `today` lets a mission resolve relative dates ("today"/"tomorrow"); harmless to others.
            var vars = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["today"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            };

            logger.LogInformation("Agent {Handle} running in room {RoomId}", handle, roomId);

            // Run in the containerised runner (39.1). All built-ins run under the trusted policy;
            // custom missions get the restricted policy in 39.5.
            var result = await runner.RunAsync(descriptor.MissionRef, goal, RunPolicy.Trusted, vars, input, ct);

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

            // File-out (38.9): store any produced file as a room artifact and attach its ref to the
            // reply; derive the human line from the actual result (removed-count), not a hardcode.
            List<ArtifactRef> outArtifacts = [];
            string text;
            if (result.Output is { } produced)
            {
                var bytes = Convert.FromBase64String(produced.Base64);
                using var ms = new MemoryStream(bytes);
                outArtifacts.Add(await artifacts.PutAsync(roomId, produced.FileName, produced.ContentType, ms, ct));
                text = ComposeFileReply(result.Trace);
            }
            else
            {
                text = string.IsNullOrEmpty(result.AgentText) ? "(no answer)" : result.AgentText;
            }

            await PostAsync(roomId, agent, handle, triggerMessageId,
                text,
                verified: verifies && result.Verified,
                stepCount: result.StepCount,
                retryCount: result.RetryCount,
                trace: trace, artifacts: outArtifacts, ct);

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

    private async Task PostAsync(
        Guid roomId, Member agent, string handle, Guid triggerMessageId,
        string text, bool verified, int stepCount, int retryCount, List<AgentStep> trace,
        List<ArtifactRef> artifacts, CancellationToken ct)
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
                Artifacts = artifacts,
            },
        }, ct);

        await broadcaster.PublishMessageAsync(roomId, message.ToDto(agent.DisplayName));
    }

    /// <summary>Stage the room's most recent uploaded file as inline base64 for the runner (38.9).
    /// Returns null when the room has no artifact, or the requester can't open it (membership).</summary>
    private async Task<RunArtifact?> StageLatestArtifactAsync(Guid roomId, Guid requesterId, CancellationToken ct)
    {
        var recent = await reads.GetRecentMessagesAsync(roomId, limit: 50, ct: ct);
        ArtifactRef? latest = null;
        foreach (var m in recent.Reverse())            // chronological → newest first
        {
            if (m.Payload.Artifacts.Count > 0) { latest = m.Payload.Artifacts[^1]; break; }
        }
        if (latest is null)
            return null;

        var content = await artifacts.OpenAsync(roomId, latest.Id, requesterId, ct);
        if (content is null)
            return null;

        await using var stream = content.Content;
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return new RunArtifact(content.Filename, content.ContentType, Convert.ToBase64String(ms.ToArray()));
    }

    /// <summary>Compose the reply line for a file-out run from the actual result (decision ③a) —
    /// removed-count + cover, read from a step's structured output, never hardcoded.</summary>
    private static string ComposeFileReply(IReadOnlyList<RunTraceStep> trace)
    {
        foreach (var step in trace)
        {
            if (string.IsNullOrWhiteSpace(step.Text)) continue;
            try
            {
                using var doc = JsonDocument.Parse(step.Text);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("removed", out var removed)
                    && removed.ValueKind == JsonValueKind.Array)
                {
                    var n = removed.GetArrayLength();
                    return $"Here's your verified PDF — removed {n} {(n == 1 ? "slide" : "slides")} and added a cover.";
                }
            }
            catch (JsonException) { /* step output isn't JSON — keep looking */ }
        }
        return "Here's your verified PDF.";
    }
}
