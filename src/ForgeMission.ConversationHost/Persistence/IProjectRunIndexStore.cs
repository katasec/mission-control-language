using ForgeMission.ConversationHost.Grains;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Persistence;

/// <summary>Host-owned, rebuildable query index for Project Mission event history.</summary>
public interface IProjectRunIndexStore
{
    Task<ProjectRunIndexCheckpoint> ReadCheckpointAsync(ConversationAddress address, CancellationToken ct);
    Task<ProjectRunSummary?> FindRunAsync(ConversationAddress address, Guid runId, CancellationToken ct);
    Task<ProjectRunIndexCheckpoint> CommitBatchAsync(
        ConversationAddress address, ProjectRunIndexCheckpoint expectedCheckpoint,
        ProjectRunSummary[] summaries, long nextSequence, CancellationToken ct);
    Task<ProjectRunSummary[]> ReadPageAsync(
        ConversationAddress address, long anchorSequence, long? beforeAcceptedSequence, int count, CancellationToken ct);
}

/// <summary>The index version and durable cursor. A null ETag denotes an absent checkpoint.</summary>
public sealed record ProjectRunIndexCheckpoint(int Version, long IndexedSequence, string? ETag);
