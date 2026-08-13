using System.Buffers;
using System.Text.Json;
using ForgeMission.ConversationWorker.Janus;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Core.Runtime;
using Microsoft.Extensions.AI;

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
public sealed class MissionCommandProcessor(JanusMissionContext mission)
{
    public async Task<WorkerSessionState> ProcessAsync(
        ConversationCommand command,
        string tenantId,
        WorkerSessionState? session,
        Func<WorkerSessionState, CancellationToken, Task> saveSessionAsync,
        Func<ConversationProgress, string, CancellationToken, Task> publishAsync,
        CancellationToken ct)
    {
        // A crash between persisting a pending progress fact and its confirmed send resends the
        // identical fact, under its already-assigned deterministic ID, before anything else runs.
        if (session is { PendingProgressJson: { } pendingJson })
        {
            var pending = JsonSerializer.Deserialize(pendingJson, ConversationContractsJsonContext.Default.ConversationProgress)
                ?? throw new InvalidOperationException("PendingProgressJson deserialized to null.");
            await publishAsync(pending, tenantId, ct);
            session = session with { PendingProgressJson = null, NextProgressOrdinal = session.NextProgressOrdinal + 1 };
            await saveSessionAsync(session, ct);
        }

        async Task<WorkerSessionState> PublishFactDurablyAsync(WorkerSessionState current, ConversationProgress progress, CancellationToken factCt)
        {
            var withPending = current with
            {
                PendingProgressJson = JsonSerializer.Serialize(progress, ConversationContractsJsonContext.Default.ConversationProgress),
            };
            await saveSessionAsync(withPending, factCt);
            await publishAsync(progress, tenantId, factCt);
            var cleared = withPending with { PendingProgressJson = null, NextProgressOrdinal = withPending.NextProgressOrdinal + 1 };
            await saveSessionAsync(cleared, factCt);
            return cleared;
        }

        if (session is { } redelivered && redelivered.CurrentCommandId == command.CommandId)
        {
            if (redelivered.Phase != WorkerSessionPhase.ExecutingProvider)
                return redelivered; // WaitingForTool or Terminal: a plain redelivery — no-op.

            var interrupted = new ConversationProgress(
                ConversationDeterministicIds.Progress(command.CommandId, redelivered.NextProgressOrdinal),
                command.ConversationId, command.RunId, ConversationEventKind.RunStatus, ConversationParticipant.Forge,
                null, null, null, null, null, null, null, ConversationRunStatus.Interrupted, DateTimeOffset.UtcNow);
            var afterInterrupt = await PublishFactDurablyAsync(redelivered, interrupted, ct);
            afterInterrupt = afterInterrupt with { Phase = WorkerSessionPhase.Terminal };
            await saveSessionAsync(afterInterrupt, ct);
            return afterInterrupt;
        }

        if (command.Kind == ConversationCommandKind.ContinueAfterTool)
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

            var toolRequestId = ConversationDeterministicIds.ToolRequest(command.CommandId, 0);

            async Task PublishMappedFactAsync(MappedProgressFact fact, CancellationToken factCt)
            {
                var eventId = ConversationDeterministicIds.Progress(command.CommandId, state.NextProgressOrdinal);
                var progress = new ConversationProgress(
                    eventId, command.ConversationId, command.RunId, fact.Kind, fact.Participant, fact.Attempt,
                    fact.Text, fact.Reason, fact.Approval, fact.ToolRequest, null, null, null, DateTimeOffset.UtcNow);
                state = await PublishFactDurablyAsync(state, progress, factCt);
            }

            var result = await JanusMissionExecutor.RunContinuationAsync(
                mission, state.ApprovedPlan!, outstanding.ProviderCallId, outstanding.ToolName, outstanding.Arguments,
                command.ToolResult, command.Capabilities, toolRequestId, PublishMappedFactAsync, ct);

            return await HandleMissionResultAsync(command, state, ToOutcome(result, toolRequestId), PublishFactDurablyAsync, saveSessionAsync, ct);
        }

        // command.Kind == StartMission.
        if (session is { Phase: not WorkerSessionPhase.Terminal })
            return session; // A duplicate/second StartMission while a run is already active — no-op.

        var fresh = new WorkerSessionState(command.CommandId, command.RunId, WorkerSessionPhase.ExecutingProvider, 0, null, null, null);
        await saveSessionAsync(fresh, ct);
        var freshState = fresh;

        var freshToolRequestId = ConversationDeterministicIds.ToolRequest(command.CommandId, 0);

        async Task PublishFreshFactAsync(MappedProgressFact fact, CancellationToken factCt)
        {
            var eventId = ConversationDeterministicIds.Progress(command.CommandId, freshState.NextProgressOrdinal);
            var progress = new ConversationProgress(
                eventId, command.ConversationId, command.RunId, fact.Kind, fact.Participant, fact.Attempt,
                fact.Text, fact.Reason, fact.Approval, fact.ToolRequest, null, null, null, DateTimeOffset.UtcNow);
            freshState = await PublishFactDurablyAsync(freshState, progress, factCt);
        }

        Task OnApprovedPlanAsync(string plan, CancellationToken planCt)
        {
            freshState = freshState with { ApprovedPlan = plan };
            return saveSessionAsync(freshState, planCt);
        }

        var freshResult = await JanusMissionExecutor.RunFullMissionAsync(
            mission, command.Goal, command.Capabilities, freshToolRequestId, PublishFreshFactAsync, OnApprovedPlanAsync, ct);

        return await HandleMissionResultAsync(command, freshState, ToOutcome(freshResult, freshToolRequestId), PublishFactDurablyAsync, saveSessionAsync, ct);
    }

    private static async Task<WorkerSessionState> HandleMissionResultAsync(
        ConversationCommand command,
        WorkerSessionState session,
        MissionResultOutcome result,
        Func<WorkerSessionState, ConversationProgress, CancellationToken, Task<WorkerSessionState>> publishFactDurablyAsync,
        Func<WorkerSessionState, CancellationToken, Task> saveSessionAsync,
        CancellationToken ct)
    {
        if (result.ToolCall is { } call)
        {
            var outstanding = new OutstandingToolCall(result.ToolRequestId, call.CallId, call.Name, ToJsonElement(call.Arguments));
            session = session with { Phase = WorkerSessionPhase.WaitingForTool, OutstandingTool = outstanding };
            await saveSessionAsync(session, ct);
            return session;
        }

        if (!result.Passed)
        {
            var error = new ConversationProgress(
                ConversationDeterministicIds.Progress(command.CommandId, session.NextProgressOrdinal),
                command.ConversationId, command.RunId, ConversationEventKind.Error, ConversationParticipant.Forge,
                null, null, result.FailReason ?? "Mission failed.", null, null, null, null, null, DateTimeOffset.UtcNow);
            session = await publishFactDurablyAsync(session, error, ct);
        }

        var runStatus = new ConversationProgress(
            ConversationDeterministicIds.Progress(command.CommandId, session.NextProgressOrdinal),
            command.ConversationId, command.RunId, ConversationEventKind.RunStatus, ConversationParticipant.Forge,
            null, null, null, null, null, null, null,
            result.Passed ? ConversationRunStatus.Completed : ConversationRunStatus.Failed, DateTimeOffset.UtcNow);
        session = await publishFactDurablyAsync(session, runStatus, ct);

        session = session with { Phase = WorkerSessionPhase.Terminal };
        await saveSessionAsync(session, ct);
        return session;
    }

    // A pause (non-empty ToolCalls) and a genuine pass/fail are mutually exclusive outcomes of one
    // MissionResult — folded into a single closed shape so HandleMissionResultAsync never has to
    // re-derive "is this a pause" from raw PipelineRunner fields.
    internal readonly record struct MissionResultOutcome(bool Passed, string? FailReason, FunctionCallContent? ToolCall, Guid ToolRequestId);

    internal static MissionResultOutcome ToOutcome(MissionResult result, Guid toolRequestId)
    {
        if (result.ToolCalls is { Count: > 0 } calls)
        {
            if (calls.Count != 1)
                throw new InvalidOperationException($"Janus v1 supports exactly one tool call per request; got {calls.Count}.");
            return new MissionResultOutcome(true, null, calls[0], toolRequestId);
        }

        return new MissionResultOutcome(result.Status == MissionStatus.Pass, result.FailReason, null, toolRequestId);
    }

    // Mirrors RunnerToolTurnMapper's arguments->JsonElement conversion (Worker cannot reference
    // ForgeMission.Runner) — the reverse of JanusMissionExecutor's ToArguments.
    internal static JsonElement ToJsonElement(IDictionary<string, object?>? arguments)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var (name, value) in arguments ?? new Dictionary<string, object?>())
            {
                writer.WritePropertyName(name);
                WriteValue(writer, value);
            }
            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                return;
            case string text:
                writer.WriteStringValue(text);
                return;
            case bool boolean:
                writer.WriteBooleanValue(boolean);
                return;
            case long integer:
                writer.WriteNumberValue(integer);
                return;
            case double number:
                writer.WriteNumberValue(number);
                return;
            case JsonElement element:
                element.WriteTo(writer);
                return;
            default:
                throw new InvalidOperationException($"Unsupported tool argument type '{value.GetType().FullName}'.");
        }
    }
}
