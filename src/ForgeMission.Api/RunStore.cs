using System.Collections.Concurrent;

namespace ForgeMission.Api;

/// <summary>
/// Backs <c>GetRun</c> (M6 — every run is addressable independently of the connection that started
/// it). Opaque key-value on purpose (not SQL-shaped): the target shape is blob storage (Ameer,
/// 2026-07-19), so a disk- or blob-backed implementation is a straight swap, not a redesign.
/// </summary>
public interface IRunStore
{
    Task SaveAsync(string runId, ExecuteMissionResponse result, CancellationToken ct);
    Task<ExecuteMissionResponse?> TryGetAsync(string runId, CancellationToken ct);
}

/// <summary>
/// Today's only <see cref="IRunStore"/> implementation: in-memory, short-TTL, per-process. Not
/// durable across restarts or multiple ACA replicas — explicitly not the long-term shape, see
/// <see cref="IRunStore"/>'s own doc comment.
/// </summary>
public sealed class InMemoryRunStore : IRunStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<string, (ExecuteMissionResponse Result, DateTimeOffset ExpiresAt)> _store = new();

    public Task SaveAsync(string runId, ExecuteMissionResponse result, CancellationToken ct)
    {
        _store[runId] = (result, DateTimeOffset.UtcNow + Ttl);
        return Task.CompletedTask;
    }

    public Task<ExecuteMissionResponse?> TryGetAsync(string runId, CancellationToken ct)
    {
        if (_store.TryGetValue(runId, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
            return Task.FromResult<ExecuteMissionResponse?>(entry.Result);
        return Task.FromResult<ExecuteMissionResponse?>(null);
    }
}
