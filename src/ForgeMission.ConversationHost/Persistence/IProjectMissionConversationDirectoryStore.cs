using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Persistence;

/// <summary>Host-owned rebuildable directory projection. It is metadata over canonical
/// conversation checkpoints, not a Project store or a transcript copy.</summary>
public interface IProjectMissionConversationDirectoryStore
{
    Task UpsertAsync(string tenantId, MissionConversationSummary summary, CancellationToken ct);
    Task<MissionConversationSummary[]> ReadAsync(string tenantId, Guid projectId, CancellationToken ct);
    Task RemoveAsync(string tenantId, Guid projectId, Guid conversationId, CancellationToken ct);
}
