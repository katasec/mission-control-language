using System.Text;
using System.Text.Json;
using ForgeMission.ConversationHost.Persistence;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Grains;

/// <summary>Bounded projection of canonical Project Mission events. It owns no canonical state and
/// advances at most one 25-event batch per caller, so an old project never turns one request into
/// an unbounded replay.</summary>
internal sealed class ProjectRunIndex(IConversationEventStore events, IProjectRunIndexStore index)
{
    internal const int AdvanceEventLimit = 25;
    internal const int TraceEventLimit = 200;

    public async Task<ProjectRunIndexCheckpoint> AdvanceOnceAsync(ConversationAddress address, long target, CancellationToken ct)
    {
        var checkpoint = await index.ReadCheckpointAsync(address, ct);
        if (checkpoint.Version != 1)
            throw new ProjectHistoryInvalidException("Unsupported Project run index version.");
        if (checkpoint.IndexedSequence >= target)
            return checkpoint;

        var batch = await events.ReadRangeAsync(address, checkpoint.IndexedSequence, target, AdvanceEventLimit, ct);
        ValidateContiguous(batch, checkpoint.IndexedSequence);
        if (batch.Length == 0)
            throw new ProjectHistoryInvalidException("Canonical event history has a sequence gap.");

        var changed = new Dictionary<Guid, ProjectRunSummary>();
        foreach (var item in batch)
            await FoldAsync(address, item, changed, ct);

        var next = batch[^1].Sequence;
        try
        {
            return await index.CommitBatchAsync(address, checkpoint, [.. changed.Values], next, ct);
        }
        catch (ProjectRunIndexConflictException)
        {
            return checkpoint;
        }
    }

    public Task<ProjectRunSummary?> FindAsync(ConversationAddress address, Guid runId, CancellationToken ct) =>
        index.FindRunAsync(address, runId, ct);

    public Task<ProjectRunSummary[]> ReadPageAsync(
        ConversationAddress address, long anchor, long? before, CancellationToken ct) =>
        index.ReadPageAsync(address, anchor, before, 21, ct);

    public async Task<ProjectRunEventPage> ReadEventsAsync(
        ConversationAddress address, Guid runId, long after, long through, CancellationToken ct)
    {
        var page = await events.ReadRangeAsync(address, after, through, TraceEventLimit, ct);
        ValidateContiguous(page, after);
        var scanned = page.Length == 0 ? after : page[^1].Sequence;
        return new ProjectRunEventPage(address.ConversationId, runId, through, scanned,
            page.Where(x => x.RunId == runId).ToArray(), scanned < through);
    }

    public async Task<ProjectCommandReceipt?> FindCommandAsync(ConversationAddress address, Guid commandId, CancellationToken ct)
    {
        var stored = await events.FindByEventIdAsync(address, commandId, ct);
        if (stored?.AcceptedCommandJson is null || stored.Event.Kind != ConversationEventKind.UserMessage)
            return null;
        var command = DeserializeCommand(stored.AcceptedCommandJson);
        if (command.Kind != ConversationCommandKind.StartMission || command.RunId is not { } runId ||
            command.CommandId != commandId || command.ConversationId != address.ConversationId ||
            !ProjectMissionNames.IsKnown(command.MissionRef) || command.Capabilities.Length != 0 || command.ProjectGoal is null)
            throw new ProjectHistoryInvalidException("Stored Project command is invalid.");
        return new ProjectCommandReceipt(address.ConversationId, runId, command.MissionRef, command.Goal,
            command.ProjectGoal, stored.Event.Sequence + 1, ConversationRunStatus.Queued);
    }

    private async Task FoldAsync(
        ConversationAddress address, ConversationEvent item, Dictionary<Guid, ProjectRunSummary> changed, CancellationToken ct)
    {
        if (item.RunId is not { } runId)
            return;

        var summary = changed.TryGetValue(runId, out var cached)
            ? cached
            : await index.FindRunAsync(address, runId, ct);

        if (item.Kind == ConversationEventKind.UserMessage)
        {
            if (summary is not null)
                throw new ProjectHistoryInvalidException("A Project run has more than one accepted input.");
            var stored = await events.FindByEventIdAsync(address, item.EventId, ct);
            if (stored?.AcceptedCommandJson is null)
                throw new ProjectHistoryInvalidException("Project run input has no accepted command.");
            var command = DeserializeCommand(stored.AcceptedCommandJson);
            ValidateStart(address, item, command, runId);
            changed[runId] = new ProjectRunSummary(runId, command.CommandId, command.MissionRef, Title(command.Goal),
                item.Sequence, item.Sequence, ConversationRunStatus.Queued, 0, 0, item.OccurredAtUtc);
            return;
        }

        if (summary is null)
            throw new ProjectHistoryInvalidException("Project run event precedes its accepted input.");
        if (item.Sequence <= summary.LastSequence)
            return;

        changed[runId] = item.Kind switch
        {
            ConversationEventKind.ParticipantMessage when item.Participant != ConversationParticipant.User =>
                summary with { ExpertTurns = summary.ExpertTurns + 1, LastSequence = item.Sequence },
            ConversationEventKind.ToolRequested => summary with { ToolCalls = summary.ToolCalls + 1, LastSequence = item.Sequence },
            ConversationEventKind.RunStatus when item.RunStatus is { } status =>
                summary with { Status = status, LastSequence = item.Sequence },
            _ => summary with { LastSequence = item.Sequence },
        };
    }

    private static void ValidateContiguous(ConversationEvent[] events, long after)
    {
        var expected = after + 1;
        foreach (var item in events)
        {
            if (item.Sequence != expected++)
                throw new ProjectHistoryInvalidException("Canonical event history is not contiguous.");
        }
    }

    private static ConversationCommand DeserializeCommand(string json) =>
        JsonSerializer.Deserialize(json, ConversationContractsJsonContext.Default.ConversationCommand)
        ?? throw new ProjectHistoryInvalidException("Accepted command did not deserialize.");

    private static void ValidateStart(ConversationAddress address, ConversationEvent item, ConversationCommand command, Guid runId)
    {
        if (command.CommandId != item.EventId || command.ConversationId != address.ConversationId || command.RunId != runId ||
            command.Kind != ConversationCommandKind.StartMission || !ProjectMissionNames.IsKnown(command.MissionRef) ||
            command.Capabilities.Length != 0 || command.ProjectGoal is null)
            throw new ProjectHistoryInvalidException("Project run input does not match its accepted command.");
    }

    private static string Title(string input)
    {
        var collapsed = string.Join(" ", input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var scalars = collapsed.EnumerateRunes().Take(120).ToArray();
        return scalars.Length == collapsed.EnumerateRunes().Count() ? collapsed : string.Concat(scalars) + "…";
    }
}

internal sealed class ProjectHistoryInvalidException(string message) : Exception(message);
