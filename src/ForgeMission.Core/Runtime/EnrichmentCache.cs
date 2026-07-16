using System.Collections.Concurrent;

namespace ForgeMission.Core.Runtime;

// The pre-agent segment's output, keyed by conversation-prefix hash (P), so tool
// continuations resume the agent WITHOUT re-running (or losing) the enrichment.
// Injectable seam: in-proc locally; 42.6 swaps in a shared store for multi-replica cloud.
// A miss means re-run the pre-agent segment — never answer ungrounded.
public interface IEnrichmentCache
{
    Task<IReadOnlyDictionary<string, string>?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, IReadOnlyDictionary<string, string> snapshot, CancellationToken ct = default);
}

public sealed class InMemoryEnrichmentCache(TimeSpan? ttl = null, int maxEntries = 1000) : IEnrichmentCache
{
    private readonly TimeSpan _ttl = ttl ?? TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<string, (DateTimeOffset At, IReadOnlyDictionary<string, string> Snapshot)> _entries = new();

    public Task<IReadOnlyDictionary<string, string>?> GetAsync(string key, CancellationToken ct = default)
    {
        if (_entries.TryGetValue(key, out var entry) && DateTimeOffset.UtcNow - entry.At < _ttl)
            return Task.FromResult<IReadOnlyDictionary<string, string>?>(entry.Snapshot);

        _entries.TryRemove(key, out _);
        return Task.FromResult<IReadOnlyDictionary<string, string>?>(null);
    }

    public Task SetAsync(string key, IReadOnlyDictionary<string, string> snapshot, CancellationToken ct = default)
    {
        Prune();
        _entries[key] = (DateTimeOffset.UtcNow, snapshot);
        return Task.CompletedTask;
    }

    // Bound memory: drop expired entries; if still over the cap, drop the oldest.
    private void Prune()
    {
        if (_entries.Count < maxEntries) return;

        var now = DateTimeOffset.UtcNow;
        foreach (var (key, entry) in _entries)
            if (now - entry.At >= _ttl)
                _entries.TryRemove(key, out _);

        while (_entries.Count >= maxEntries)
        {
            var oldest = _entries.MinBy(e => e.Value.At);
            _entries.TryRemove(oldest.Key, out _);
        }
    }
}
