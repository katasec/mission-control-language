using System.Text.Json;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Core.Runtime;

namespace ForgeMission.ConversationWorker.Messaging;

/// <summary>
/// SDK-independent generic command processor. The only recovery state is one command boundary
/// plus a one-fact outbox. A redelivery after the provider boundary is terminally Interrupted;
/// it never recreates a provider request from a transcript.
/// </summary>
public sealed class MissionCommandProcessor(IExpertRunner defaultRunner)
{
    private readonly GenericDurableMissionExecutor _executor = new(defaultRunner);
    private sealed class WorkerOutboxFailureException(Exception inner) : Exception(inner.Message, inner);

    public async Task<WorkerSessionState> ProcessAsync(
        ConversationCommand command, string tenantId, WorkerSessionState? session,
        Func<WorkerSessionState, CancellationToken, Task> saveSessionAsync,
        Func<ConversationProgress, string, CancellationToken, Task> publishAsync, CancellationToken ct)
    {
        async Task Save(WorkerSessionState state, CancellationToken innerCt)
        {
            try { await saveSessionAsync(state, innerCt); }
            catch (Exception exception) { throw new WorkerOutboxFailureException(exception); }
        }
        async Task Publish(ConversationProgress progress, CancellationToken innerCt)
        {
            try { await publishAsync(progress, tenantId, innerCt); }
            catch (Exception exception) { throw new WorkerOutboxFailureException(exception); }
        }
        async Task<WorkerSessionState> Send(WorkerSessionState current, ConversationProgress progress, CancellationToken innerCt)
        {
            var pending = current with { PendingProgressJson = JsonSerializer.Serialize(progress, ConversationContractsJsonContext.Default.ConversationProgress) };
            await Save(pending, innerCt);
            await Publish(progress, innerCt);
            var sent = pending with { PendingProgressJson = null, NextProgressOrdinal = pending.NextProgressOrdinal + 1 };
            await Save(sent, innerCt);
            return sent;
        }

        if (session?.PendingProgressJson is { } pendingJson)
        {
            var pending = JsonSerializer.Deserialize(pendingJson, ConversationContractsJsonContext.Default.ConversationProgress)
                ?? throw new InvalidOperationException("Pending progress deserialized to null.");
            await Publish(pending, ct);
            session = session with { PendingProgressJson = null, NextProgressOrdinal = session.NextProgressOrdinal + 1 };
            await Save(session, ct);
        }
        if (session is { } replay && replay.CurrentCommandId == command.CommandId)
        {
            if (replay.Phase != WorkerSessionPhase.ExecutingProvider) return replay;
            var terminal = await Send(replay, Progress(command, replay.NextProgressOrdinal, ConversationEventKind.RunStatus,
                ConversationParticipant.Forge, runStatus: ConversationRunStatus.Interrupted), ct);
            terminal = terminal with { Phase = WorkerSessionPhase.Terminal };
            await Save(terminal, ct);
            return terminal;
        }
        if (command.Kind == ConversationCommandKind.ContinueAfterTool)
            return await ContinueAsync(command, session, Save, Send, ct);
        if (command.Kind != ConversationCommandKind.StartMission || session is { Phase: not WorkerSessionPhase.Terminal })
            return session ?? new WorkerSessionState(command.CommandId, command.RunId, WorkerSessionPhase.Terminal, 0, null);

        string? packageReason = null;
        if (command.RunId is null || command.Launch?.Package is null ||
            !GenericDurableMissionExecutor.TryPackage(command.Launch.Package, out _, out packageReason))
        {
            var rejected = new WorkerSessionState(command.CommandId, command.RunId, WorkerSessionPhase.ExecutingProvider, 0, null);
            await Save(rejected, ct);
            return await CompleteAsync(command, rejected,
                new MissionResult(command.MissionRef, string.Empty, MissionStatus.Fail, packageReason ?? "Unsupported legacy mission command."), Send, Save, ct);
        }

        var state = new WorkerSessionState(command.CommandId, command.RunId, WorkerSessionPhase.ExecutingProvider, 0, null,
            command.Launch.Package.PackageHash);
        await Save(state, ct);
        async Task Trace(PipelineTraceEvent trace, CancellationToken innerCt)
        {
            ConversationProgress? fact = trace switch
            {
                PipelineStepStarted started => Progress(command, state.NextProgressOrdinal, ConversationEventKind.ParticipantStarted,
                    ConversationParticipant.Forge, started.Attempt, text: $"{started.MissionName}:{started.ExpertName}",
                    actorName: started.ExpertName),
                PipelineStepCompleted completed => Progress(command, state.NextProgressOrdinal,
                    string.Equals(completed.Envelope.Status, "pass", StringComparison.OrdinalIgnoreCase)
                        ? ConversationEventKind.ParticipantMessage : ConversationEventKind.Error,
                    ConversationParticipant.Forge, completed.Attempt,
                    text: string.Equals(completed.Envelope.Status, "pass", StringComparison.OrdinalIgnoreCase) ? completed.Envelope.Text : null,
                    reason: string.Equals(completed.Envelope.Status, "pass", StringComparison.OrdinalIgnoreCase) ? null : completed.Envelope.Reason ?? completed.Envelope.Text,
                    actorName: completed.ExpertName),
                _ => null,
            };
            if (fact is not null) state = await Send(state, fact, innerCt);
        }

        MissionResult result;
        try { result = await _executor.RunAsync(command.Launch.Package, command.Goal, command.Launch.Profile, Trace, ct); }
        catch (Exception exception) when (!ct.IsCancellationRequested && exception is not WorkerOutboxFailureException)
        { result = new MissionResult(command.MissionRef, string.Empty, MissionStatus.Fail, exception.Message); }
        if (result.Pause is { } pause)
            return await PauseAsync(command, state, pause, Send, Save, ct);
        return await CompleteAsync(command, state, result, Send, Save, ct);
    }

    private async Task<WorkerSessionState> ContinueAsync(ConversationCommand command, WorkerSessionState? session,
        Func<WorkerSessionState, CancellationToken, Task> save,
        Func<WorkerSessionState, ConversationProgress, CancellationToken, Task<WorkerSessionState>> send, CancellationToken ct)
    {
        if (session is null || session.Phase != WorkerSessionPhase.WaitingForHands || session.RunId != command.RunId ||
            command.Launch?.Package is null || command.ToolResult is null || string.IsNullOrWhiteSpace(command.OpaqueContinuation) ||
            string.IsNullOrWhiteSpace(command.ProviderToolCallId))
            return session ?? new WorkerSessionState(command.CommandId, command.RunId, WorkerSessionPhase.Terminal, 0, null);
        var state = session with { CurrentCommandId = command.CommandId, Phase = WorkerSessionPhase.ExecutingProvider, OpaqueContinuation = null };
        await save(state, ct);
        async Task Trace(PipelineTraceEvent trace, CancellationToken innerCt)
        {
            ConversationProgress? fact = trace switch
            {
                PipelineStepStarted started => Progress(command, state.NextProgressOrdinal, ConversationEventKind.ParticipantStarted,
                    ConversationParticipant.Forge, started.Attempt, text: $"{started.MissionName}:{started.ExpertName}",
                    actorName: started.ExpertName),
                PipelineStepCompleted completed => Progress(command, state.NextProgressOrdinal,
                    string.Equals(completed.Envelope.Status, "pass", StringComparison.OrdinalIgnoreCase) ? ConversationEventKind.ParticipantMessage : ConversationEventKind.Error,
                    ConversationParticipant.Forge, completed.Attempt,
                    text: string.Equals(completed.Envelope.Status, "pass", StringComparison.OrdinalIgnoreCase) ? completed.Envelope.Text : null,
                    reason: string.Equals(completed.Envelope.Status, "pass", StringComparison.OrdinalIgnoreCase) ? null : completed.Envelope.Reason ?? completed.Envelope.Text,
                    actorName: completed.ExpertName),
                _ => null,
            };
            if (fact is not null) state = await send(state, fact, innerCt);
        }
        MissionResult result;
        try { result = await _executor.ResumeAsync(command.Launch.Package, command.Launch.Profile, command.OpaqueContinuation,
            command.ProviderToolCallId, command.ToolResult, Trace, ct); }
        catch (Exception exception) when (!ct.IsCancellationRequested && exception is not WorkerOutboxFailureException)
        { result = new MissionResult(command.MissionRef, string.Empty, MissionStatus.Fail, exception.Message); }
        if (result.Pause is { } pause) return await PauseAsync(command, state, pause, send, save, ct);
        return await CompleteAsync(command, state, result, send, save, ct);
    }

    private static async Task<WorkerSessionState> PauseAsync(ConversationCommand command, WorkerSessionState state,
        PipelineToolPause pause, Func<WorkerSessionState, ConversationProgress, CancellationToken, Task<WorkerSessionState>> send,
        Func<WorkerSessionState, CancellationToken, Task> save, CancellationToken ct)
    {
        var requestId = ConversationDeterministicIds.MissionHandsRequest(command.ConversationId, command.CommandId, state.NextProgressOrdinal);
        var request = new MissionToolRequest(requestId, command.ConversationId, command.CommandId,
            string.Join('/', pause.MissionPath), pause.ExpertName, pause.ToolCall.CallId, pause.ToolCall.Name,
            pause.ToolCall.Arguments, pause.Continuation.Payload);
        state = state with { Phase = WorkerSessionPhase.WaitingForHands, OpaqueContinuation = pause.Continuation.Payload };
        await save(state, ct);
        var progress = Progress(command, state.NextProgressOrdinal, ConversationEventKind.MissionHandsRequested,
            ConversationParticipant.Forge, attempt: pause.Attempt, missionHandsRequest: request) with { EventId = requestId };
        return await send(state, progress, ct);
    }

    private static async Task<WorkerSessionState> CompleteAsync(ConversationCommand command, WorkerSessionState state,
        MissionResult result, Func<WorkerSessionState, ConversationProgress, CancellationToken, Task<WorkerSessionState>> send,
        Func<WorkerSessionState, CancellationToken, Task> save, CancellationToken ct)
    {
        if (result.Status == MissionStatus.Fail)
            state = await send(state, Progress(command, state.NextProgressOrdinal, ConversationEventKind.Error,
                ConversationParticipant.Forge, reason: result.FailReason ?? "Generic mission failed."), ct);
        else if (!string.IsNullOrWhiteSpace(result.Text))
            state = await send(state, Progress(command, state.NextProgressOrdinal, ConversationEventKind.ParticipantMessage,
                ConversationParticipant.Forge, text: result.Text), ct);
        state = await send(state, Progress(command, state.NextProgressOrdinal, ConversationEventKind.RunStatus,
            ConversationParticipant.Forge, runStatus: result.Status == MissionStatus.Pass ? ConversationRunStatus.Completed : ConversationRunStatus.Failed), ct);
        state = state with { Phase = WorkerSessionPhase.Terminal };
        await save(state, ct);
        return state;
    }

    // actorName is the trace's own expert name, copied verbatim for a started/completed step. This
    // Worker maps no mission name, chooses no persona, and branches on no mission: the name is a
    // generic execution fact the durable stream carries onward unchanged.
    private static ConversationProgress Progress(ConversationCommand command, int ordinal, ConversationEventKind kind,
        ConversationParticipant participant, int? attempt = null, string? text = null, string? reason = null,
        ConversationRunStatus? runStatus = null, MissionToolRequest? missionHandsRequest = null, string? actorName = null) => new(
            ConversationDeterministicIds.Progress(command.CommandId, ordinal), command.ConversationId, command.RunId,
            kind, participant, attempt, text, reason, null, null, null, null, runStatus, DateTimeOffset.UtcNow, missionHandsRequest,
            actorName);
}
