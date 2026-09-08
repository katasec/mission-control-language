using System.Text.Json;
using Azure.Data.Tables;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Persistence;

public sealed class AzureTableProjectMissionConversationDirectoryStore(TableServiceClient tables, ConversationStorageOptions options)
    : IProjectMissionConversationDirectoryStore
{
    private readonly TableClient table = tables.GetTableClient(options.EventTableName);

    public Task UpsertAsync(string tenantId, MissionConversationSummary summary, CancellationToken ct) =>
        table.UpsertEntityAsync(new TableEntity(Partition(tenantId, summary.ProjectId), Row(summary.ConversationId))
        {
            ["SummaryJson"] = JsonSerializer.Serialize(summary, ConversationContractsJsonContext.Default.MissionConversationSummary),
        }, TableUpdateMode.Replace, ct);

    public async Task<MissionConversationSummary[]> ReadAsync(string tenantId, Guid projectId, CancellationToken ct)
    {
        var values = new List<MissionConversationSummary>();
        await foreach (var entity in table.QueryAsync<TableEntity>(TableClient.CreateQueryFilter($"PartitionKey eq {Partition(tenantId, projectId)}"), cancellationToken: ct))
        {
            var value = JsonSerializer.Deserialize(entity.GetString("SummaryJson"), ConversationContractsJsonContext.Default.MissionConversationSummary);
            if (value is not null) values.Add(value);
        }
        return [.. values.OrderByDescending(value => value.UpdatedAtUtc)];
    }

    public async Task RemoveAsync(string tenantId, Guid projectId, Guid conversationId, CancellationToken ct) =>
        await table.DeleteEntityAsync(Partition(tenantId, projectId), Row(conversationId), cancellationToken: ct);

    private static string Partition(string tenantId, Guid projectId) => $"5-mission-directory:{tenantId}:{projectId:N}";
    private static string Row(Guid conversationId) => $"0-{conversationId:N}";
}
