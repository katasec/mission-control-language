using System.Text.Json;
using ForgeMission.ConversationWorker.Janus;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Core.Runtime;

namespace ForgeMission.ConversationWorker.Messaging;

/// <summary>
/// The SDK-independent core of mission-command handling: session-recovery decisions, the
/// per-fact outbox (persist pending → send → clear/increment), and driving
/// <see cref="JanusMissionExecutor"/>. Deliberately takes plain delegates for session
/// persistence and progress publishing rather than any Service Bus SDK type, so this class is
/// exercised directly in tests with in-memory fakes — <c>ProcessSessionMessageEventArgs</c> and
/// <c>ServiceBusSessionReceiver</c> have no public constructor, so the SDK adapter
/// (<see cref="AzureServiceBusMissionCommandConsumer"/>) that wraps this class is a thin,
/// deliberately untested shim. Returns the session state a caller should treat as current after
/// the call (also the last state persisted via <paramref name="saveSessionAsync"/> below) — a
/// caller with no other need for it may ignore the return value.
/// </summary>
public sealed class MissionCommandProcessor(WorkerMissionResolver missions)
{

    // Marks a failure from the save/publish delegates themselves (a transient store/broker
    // problem) as distinct from a caught Janus/provider execution failure. The two try/catch
    // blocks in ProcessFreshStartAsync/ProcessContinuationAsync deliberately exclude this type —
    // a save/publish failure must never be reclassified into a synthetic Error/Failed fact (doing
    // so could overwrite a pending progress fact already persisted for an earlier, still-unsent
    // send). It must escape ProcessAsync entirely so the caller leaves the command unsettled and
    // the last durably-saved state (including any still-pending fact) is exactly what a normal
    // retry/redelivery finds.
    private sealed class WorkerOutboxFailureException(Exception inner) : Exception(inner.Message, inner);

    public async Task<WorkerSessionState> ProcessAsync(
        ConversationCommand command,
        string tenantId,
        WorkerSessionState? session,
        Func<WorkerSessionState, CancellationToken, Task> saveSessionAsync,
        Func<ConversationProgress, string, CancellationToken, Task> publishAsync,
        CancellationToken ct)
    {
        // Every downstream call goes through these guarded wrappers instead of the raw delegates,
        // so a failure at the save/publish boundary is uniformly tagged no matter where in the
        // outbox flow it happens.
        async Task GuardedSaveAsync(WorkerSessionState state, CancellationToken saveCt)
        {
            try { await saveSessionAsync(state, saveCt); }
            catch (Exception ex) { throw new WorkerOutboxFailureException(ex); }
        }

        async Task GuardedPublishAsync(ConversationProgress progress, string tid, CancellationToken publishCt)
        {
            try { await publishAsync(progress, tid, publishCt); }
            catch (Exception ex) { throw new WorkerOutboxFailureException(ex); }
        }

        // A crash between persisting a pending progress fact and its confirmed send resends the
        // identical fact, under its already-assigned deterministic ID, before anything else runs.
        if (session is { PendingProgressJson: { } pendingJson })
        {
            var pending = JsonSerializer.Deserialize(pendingJson, ConversationContractsJsonContext.Default.ConversationProgress)
                ?? throw new InvalidOperationException("PendingProgressJson deserialized to null.");
            await GuardedPublishAsync(pending, tenantId, ct);
            session = session with { PendingProgressJson = null, NextProgressOrdinal = session.NextProgressOrdinal + 1 };
            await GuardedSaveAsync(session, ct);
        }

        async Task<WorkerSessionState> PublishFactDurablyAsync(WorkerSessionState current, ConversationProgress progress, CancellationToken factCt)
        {
            var withPending = current with
            {
                PendingProgressJson = JsonSerializer.Serialize(progress, ConversationContractsJsonContext.Default.ConversationProgress),
            };
            await GuardedSaveAsync(withPending, factCt);
            await GuardedPublishAsync(progress, tenantId, factCt);
            var cleared = withPending with { PendingProgressJson = null, NextProgressOrdinal = withPending.NextProgressOrdinal + 1 };
            await GuardedSaveAsync(cleared, factCt);
            return cleared;
        }

        // Resolved BEFORE any recovery or dispatch decision below: which mission this command names
        // determines not only how it executes but which facts are even legal for it to produce.
        var missionKind = missions.Resolve(command.MissionRef);

        if (session is { } redelivered && redelivered.CurrentCommandId == command.CommandId)
        {
            if (redelivered.Phase != WorkerSessionPhase.ExecutingProvider)
                return redelivered; // WaitingForTool or Terminal: a plain redelivery — no-op.

            // A redelivery after a process failure is an interrupted mission run. Unknown
            // historical command refs are already resolved as Unsupported and still receive the
            // same durable terminal fact; no legacy control branch can execute here.
            var interruptionFact = BuildProgress(
                command, redelivered.NextProgressOrdinal, ConversationEventKind.RunStatus, ConversationParticipant.Forge,
                runStatus: ConversationRunStatus.Interrupted);

            var afterInterrupt = await PublishFactDurablyAsync(redelivered, interruptionFact, ct);
            afterInterrupt = afterInterrupt with { Phase = WorkerSessionPhase.Terminal };
            await GuardedSaveAsync(afterInterrupt, ct);
            return afterInterrupt;
        }

        if (command.Kind == ConversationCommandKind.ContinueAfterTool)
            return await ProcessContinuationAsync(command, session, GuardedSaveAsync, PublishFactDurablyAsync, ct);

        // command.Kind == StartMission.
        if (session is { Phase: not WorkerSessionPhase.Terminal })
            return session; // A duplicate/second StartMission while work is already active — no-op.

        return await ProcessFreshStartAsync(command, missionKind, GuardedSaveAsync, PublishFactDurablyAsync, ct);
    }

    /// <summary>
    /// One Naive Mission Run (43.21 task 1): a single-expert answer published as an ordinary run.
    ///
    /// It is deliberately the same shape as a Janus run — a <c>ParticipantMessage</c> carrying the
    /// answer, then a terminal <c>RunStatus</c> through the shared
    /// <see cref="HandleMissionResultAsync"/> — so nothing downstream can tell the two missions
    /// apart structurally. It publishes one mission-owned message fact and then the shared terminal
    /// status, with no separate execution path.
    /// </summary>
    private async Task<WorkerSessionState> ProcessNaiveRunAsync(
        ConversationCommand command,
        WorkerSessionState state,
        Func<WorkerSessionState, CancellationToken, Task> saveSessionAsync,
        Func<WorkerSessionState, ConversationProgress, CancellationToken, Task<WorkerSessionState>> publishFactDurablyAsync,
        CancellationToken ct)
    {
        MissionResult result;
        try
        {
            // ProjectGoal is pinned container state set by ConversationGrain, never caller input.
            // An empty one is tolerated rather than fatal: unlike a control turn, a Naive run's
            // instruction is its own input, and a Project with a blank goal must still be able to
            // run one.
            result = await NaiveMissionExecutor.RunTurnAsync(
                missions.Naive, command.ProjectGoal ?? "", command.Goal, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is not WorkerOutboxFailureException)
        {
            // Same classification rule as Janus: a caught executor/provider failure is a KNOWN
            // outcome and resolves as Error+Failed, never as Interrupted.
            return await HandleMissionResultAsync(
                command, state, new MissionResult(command.MissionRef, "", MissionStatus.Fail, ex.Message),
                publishFactDurablyAsync, saveSessionAsync, ct);
        }

        if (result.Status == MissionStatus.Pass)
        {
            var answer = BuildProgress(
                command, state.NextProgressOrdinal, ConversationEventKind.ParticipantMessage,
                ConversationParticipant.Naive, text: result.Text);
            state = await publishFactDurablyAsync(state, answer, ct);
        }

        return await HandleMissionResultAsync(command, state, result, publishFactDurablyAsync, saveSessionAsync, ct);
    }

    private async Task<WorkerSessionState> ProcessFreshStartAsync(
        ConversationCommand command,
        WorkerMissionKind missionKind,
        Func<WorkerSessionState, CancellationToken, Task> saveSessionAsync,
        Func<WorkerSessionState, ConversationProgress, CancellationToken, Task<WorkerSessionState>> publishFactDurablyAsync,
        CancellationToken ct)
    {
        var state = new WorkerSessionState(command.CommandId, command.RunId, WorkerSessionPhase.ExecutingProvider, 0, null, null, null);
        await saveSessionAsync(state, ct);

        // A Naive run goes through the SAME start/terminal protocol as a Janus one — it differs
        // only in what happens between them, which is the whole point of 43.21 task 1: two
        // missions, one run shape.
        if (missionKind == WorkerMissionKind.Naive)
            return await ProcessNaiveRunAsync(command, state, saveSessionAsync, publishFactDurablyAsync, ct);

        if (missionKind != WorkerMissionKind.Janus)
            return await RejectUnsupportedMissionAsync(command, state, publishFactDurablyAsync, saveSessionAsync, ct);

        async Task PublishMappedFactAsync(MappedProgressFact fact, CancellationToken factCt)
            => state = await PublishFactAsync(command, state, fact, publishFactDurablyAsync, saveSessionAsync, factCt);

        Task OnApprovedPlanAsync(string plan, CancellationToken planCt)
        {
            state = state with { ApprovedPlan = plan };
            return saveSessionAsync(state, planCt);
        }

        MissionResult result;
        try
        {
            result = await JanusMissionExecutor.RunFullMissionAsync(
                missions.Janus, command.Goal, command.Capabilities, PublishMappedFactAsync, OnApprovedPlanAsync, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is not WorkerOutboxFailureException)
        {
            // A caught executor/provider failure is a KNOWN outcome, not an ambiguous crash — it
            // resolves through the normal outbox as Error+Failed, never as Interrupted. A
            // WorkerOutboxFailureException is deliberately excluded (see its own doc comment) —
            // that class of failure must propagate unsettled, never be reclassified.
            return await HandleMissionResultAsync(
                command, state, new MissionResult(command.MissionRef, "", MissionStatus.Fail, ex.Message),
                publishFactDurablyAsync, saveSessionAsync, ct);
        }

        if (result.ToolCalls is { Count: > 0 })
            return state; // Already fully persisted as WaitingForTool by PublishFactAsync's ToolRequested branch.

        return await HandleMissionResultAsync(command, state, result, publishFactDurablyAsync, saveSessionAsync, ct);
    }

    private async Task<WorkerSessionState> ProcessContinuationAsync(
        ConversationCommand command,
        WorkerSessionState? session,
        Func<WorkerSessionState, CancellationToken, Task> saveSessionAsync,
        Func<WorkerSessionState, ConversationProgress, CancellationToken, Task<WorkerSessionState>> publishFactDurablyAsync,
        CancellationToken ct)
    {
        if (session is null || session.Phase != WorkerSessionPhase.WaitingForTool || session.OutstandingTool is null
            || session.RunId != command.RunId || command.ToolResult is null
            || command.ToolResult.RequestId != session.OutstandingTool.RequestId)
            return session!; // Mismatch, wrong run, or a duplicate StartMission — no progress, no execution.

        var outstanding = session.OutstandingTool;
        var state = session with
        {
            CurrentCommandId = command.CommandId, Phase = WorkerSessionPhase.ExecutingProvider, OutstandingTool = null,
        };
        await saveSessionAsync(state, ct);

        if (missions.Resolve(command.MissionRef) != WorkerMissionKind.Janus)
            return await RejectUnsupportedMissionAsync(command, state, publishFactDurablyAsync, saveSessionAsync, ct);

        async Task PublishMappedFactAsync(MappedProgressFact fact, CancellationToken factCt)
            => state = await PublishFactAsync(command, state, fact, publishFactDurablyAsync, saveSessionAsync, factCt);

        MissionResult result;
        try
        {
            result = await JanusMissionExecutor.RunContinuationAsync(
                missions.Janus, state.ApprovedPlan!, outstanding.ProviderCallId, outstanding.ToolName, outstanding.Arguments,
                command.ToolResult, command.Capabilities, PublishMappedFactAsync, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is not WorkerOutboxFailureException)
        {
            return await HandleMissionResultAsync(
                command, state, new MissionResult(command.MissionRef, "", MissionStatus.Fail, ex.Message),
                publishFactDurablyAsync, saveSessionAsync, ct);
        }

        if (result.ToolCalls is { Count: > 0 })
            return state;

        return await HandleMissionResultAsync(command, state, result, publishFactDurablyAsync, saveSessionAsync, ct);
    }

    // The one place a ToolRequested fact is turned into a durable send — and the one place the
    // WaitingForTool/OutstandingTool crash boundary is enforced: session state naming the exact
    // tool-request correlation (deterministic RequestId, provider CallId, name, arguments) is
    // persisted BEFORE this method builds or sends the ConversationProgress fact that announces it,
    // using the SAME current NextProgressOrdinal for both the Progress EventId and the ToolRequest
    // RequestId (Janus v1 has exactly one tool request per command, so they name the same fact).
    private static async Task<WorkerSessionState> PublishFactAsync(
        ConversationCommand command,
        WorkerSessionState state,
        MappedProgressFact fact,
        Func<WorkerSessionState, ConversationProgress, CancellationToken, Task<WorkerSessionState>> publishFactDurablyAsync,
        Func<WorkerSessionState, CancellationToken, Task> saveSessionAsync,
        CancellationToken ct)
    {
        if (fact.Kind == ConversationEventKind.ToolRequested)
        {
            var ordinal = state.NextProgressOrdinal;
            var toolRequestId = ConversationDeterministicIds.ToolRequest(command.CommandId, ordinal);
            var outstanding = new OutstandingToolCall(toolRequestId, fact.ProviderCallId!, fact.ToolName!, fact.ToolArguments!.Value);

            state = state with { Phase = WorkerSessionPhase.WaitingForTool, OutstandingTool = outstanding };
            await saveSessionAsync(state, ct);

            var toolRequestProgress = BuildProgress(
                command, ordinal, fact.Kind, fact.Participant, fact.Attempt,
                toolRequest: new ConversationToolRequest(toolRequestId, fact.ToolName!, fact.ToolArguments!.Value));
            return await publishFactDurablyAsync(state, toolRequestProgress, ct);
        }

        var progress = BuildProgress(
            command, state.NextProgressOrdinal, fact.Kind, fact.Participant, fact.Attempt, fact.Text, fact.Reason, fact.Approval);
        return await publishFactDurablyAsync(state, progress, ct);
    }

    private static Task<WorkerSessionState> RejectUnsupportedMissionAsync(
        ConversationCommand command,
        WorkerSessionState state,
        Func<WorkerSessionState, ConversationProgress, CancellationToken, Task<WorkerSessionState>> publishFactDurablyAsync,
        Func<WorkerSessionState, CancellationToken, Task> saveSessionAsync,
        CancellationToken ct)
        => HandleMissionResultAsync(
            command, state,
            new MissionResult(command.MissionRef, "", MissionStatus.Fail,
                $"Unsupported MissionRef '{command.MissionRef}'."),
            publishFactDurablyAsync, saveSessionAsync, ct);

    private static async Task<WorkerSessionState> HandleMissionResultAsync(
        ConversationCommand command,
        WorkerSessionState session,
        MissionResult result,
        Func<WorkerSessionState, ConversationProgress, CancellationToken, Task<WorkerSessionState>> publishFactDurablyAsync,
        Func<WorkerSessionState, CancellationToken, Task> saveSessionAsync,
        CancellationToken ct)
    {
        if (result.Status == MissionStatus.Fail)
        {
            var error = BuildProgress(
                command, session.NextProgressOrdinal, ConversationEventKind.Error, ConversationParticipant.Forge,
                reason: result.FailReason ?? "Mission failed.");
            session = await publishFactDurablyAsync(session, error, ct);
        }

        var runStatus = BuildProgress(
            command, session.NextProgressOrdinal, ConversationEventKind.RunStatus, ConversationParticipant.Forge,
            runStatus: result.Status == MissionStatus.Pass ? ConversationRunStatus.Completed : ConversationRunStatus.Failed);
        session = await publishFactDurablyAsync(session, runStatus, ct);

        session = session with { Phase = WorkerSessionPhase.Terminal };
        await saveSessionAsync(session, ct);
        return session;
    }

    private static ConversationProgress BuildProgress(
        ConversationCommand command,
        int ordinal,
        ConversationEventKind kind,
        ConversationParticipant participant,
        int? attempt = null,
        string? text = null,
        string? reason = null,
        ConversationApproval? approval = null,
        ConversationToolRequest? toolRequest = null,
        ConversationRunStatus? runStatus = null)
        => new(
            ConversationDeterministicIds.Progress(command.CommandId, ordinal), command.ConversationId, command.RunId,
            kind, participant, attempt, text, reason, approval, toolRequest, null, null, runStatus, DateTimeOffset.UtcNow);
}
