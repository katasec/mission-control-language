using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Persistence;

/// <summary>Azure Table persistence for the derived Project Mission index. It shares the canonical
/// event table partition, while keeping its rows above the event/idempotency prefixes.</summary>
public sealed class AzureTableProjectRunIndexStore(TableServiceClient tables, ConversationStorageOptions options) : IProjectRunIndexStore
{
    private const string CheckpointKey = "4-project-run-index";
    private const string DescendingPrefix = "2-";
    private const string ByRunPrefix = "3-";
    private readonly TableClient table = tables.GetTableClient(options.EventTableName);

    public async Task<ProjectRunIndexCheckpoint> ReadCheckpointAsync(ConversationAddress address, CancellationToken ct)
    {
        var entity = await table.GetEntityIfExistsAsync<TableEntity>(address.PartitionKey, CheckpointKey, cancellationToken: ct);
        return entity.HasValue
            ? new ProjectRunIndexCheckpoint(entity.Value!.GetInt32("Version") ?? 1, entity.Value.GetInt64("IndexedSequence") ?? 0, entity.Value.ETag.ToString())
            : new ProjectRunIndexCheckpoint(1, 0, null);
    }

    public async Task<ProjectRunSummary?> FindRunAsync(ConversationAddress address, Guid runId, CancellationToken ct)
    {
        var entity = await table.GetEntityIfExistsAsync<TableEntity>(address.PartitionKey, ByRunKey(runId), cancellationToken: ct);
        return entity.HasValue ? Deserialize(entity.Value!.GetString("SummaryJson")) : null;
    }

    public async Task<ProjectRunIndexCheckpoint> CommitBatchAsync(
        ConversationAddress address, ProjectRunIndexCheckpoint expected, ProjectRunSummary[] summaries, long nextSequence, CancellationToken ct)
    {
        var actions = new List<TableTransactionAction>();
        foreach (var summary in summaries)
        {
            var json = JsonSerializer.Serialize(summary, ConversationContractsJsonContext.Default.ProjectRunSummary);
            if (System.Text.Encoding.UTF8.GetByteCount(json) > 2048)
                throw new InvalidOperationException("Project run summary exceeds its 2 KiB index limit.");
            actions.Add(new(TableTransactionActionType.UpsertReplace, SummaryEntity(address, DescendingKey(summary.AcceptedSequence), json)));
            actions.Add(new(TableTransactionActionType.UpsertReplace, SummaryEntity(address, ByRunKey(summary.RunId), json)));
        }

        var checkpoint = new TableEntity(address.PartitionKey, CheckpointKey)
        {
            ["Version"] = 1,
            ["IndexedSequence"] = nextSequence,
        };
        if (expected.ETag is null)
            actions.Add(new(TableTransactionActionType.Add, checkpoint));
        else
        {
            checkpoint.ETag = new ETag(expected.ETag);
            actions.Add(new(TableTransactionActionType.UpdateReplace, checkpoint, checkpoint.ETag));
        }

        try
        {
            await table.SubmitTransactionAsync(actions, ct);
            return await ReadCheckpointAsync(address, ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 409 || ex.Status == 412)
        {
            throw new ProjectRunIndexConflictException(ex);
        }
    }

    public async Task<ProjectRunSummary[]> ReadPageAsync(
        ConversationAddress address, long anchorSequence, long? beforeAcceptedSequence, int count, CancellationToken ct)
    {
        var start = beforeAcceptedSequence is { } before
            ? DescendingKey(before - 1)
            : DescendingPrefix;
        var end = DescendingKey(0);
        var filter = TableClient.CreateQueryFilter($"PartitionKey eq {address.PartitionKey} and RowKey ge {start} and RowKey lt {end}");
        var result = new List<ProjectRunSummary>(count);
        await foreach (var entity in table.QueryAsync<TableEntity>(filter, cancellationToken: ct))
        {
            var summary = Deserialize(entity.GetString("SummaryJson"));
            if (summary is not null && summary.AcceptedSequence <= anchorSequence)
                result.Add(summary);
            if (result.Count == count) break;
        }
        return [.. result];
    }

    private static TableEntity SummaryEntity(ConversationAddress address, string key, string json) => new(address.PartitionKey, key) { ["SummaryJson"] = json };
    private static ProjectRunSummary? Deserialize(string? json) => json is null ? null : JsonSerializer.Deserialize(json, ConversationContractsJsonContext.Default.ProjectRunSummary);
    private static string DescendingKey(long sequence) => $"{DescendingPrefix}{long.MaxValue - sequence:D19}";
    private static string ByRunKey(Guid runId) => $"{ByRunPrefix}{runId:N}";
}

public sealed class ProjectRunIndexConflictException(Exception inner) : Exception("Project run index changed concurrently.", inner);
