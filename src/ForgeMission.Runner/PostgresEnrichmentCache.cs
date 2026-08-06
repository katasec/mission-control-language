using System.Text.Json;
using ForgeMission.Core.Runtime;
using Npgsql;
using NpgsqlTypes;

namespace ForgeMission.Runner;

/// <summary>
/// Shared, Postgres-backed re-entrancy cache for the runner. Snapshots expire after thirty minutes
/// by default, matching <see cref="InMemoryEnrichmentCache"/>, and a miss deliberately causes the
/// pre-agent segment to run again rather than continuing with ungrounded context.
/// </summary>
public sealed class PostgresEnrichmentCache(NpgsqlDataSource dataSource, TimeSpan? ttl = null) : IEnrichmentCache
{
    private readonly TimeSpan _ttl = ttl ?? TimeSpan.FromMinutes(30);

    public async Task<IReadOnlyDictionary<string, string>?> GetAsync(string key, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand("""
            SELECT snapshot::text
            FROM enrichment_cache
            WHERE prefix_hash = $1 AND expires_at > NOW()
            """);
        cmd.Parameters.Add(new NpgsqlParameter { Value = key });

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is not string json) return null;

        return JsonSerializer.Deserialize(
            json,
            EnrichmentCacheJsonContext.Default.DictionaryStringString);
    }

    public async Task SetAsync(string key, IReadOnlyDictionary<string, string> snapshot, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(
            new Dictionary<string, string>(snapshot),
            EnrichmentCacheJsonContext.Default.DictionaryStringString);

        await using var cmd = dataSource.CreateCommand("""
            INSERT INTO enrichment_cache (prefix_hash, snapshot, expires_at)
            VALUES ($1, $2, $3)
            ON CONFLICT (prefix_hash) DO UPDATE
            SET snapshot = EXCLUDED.snapshot, expires_at = EXCLUDED.expires_at
            """);
        var parameters = cmd.Parameters;
        parameters.Add(new NpgsqlParameter { Value = key });
        parameters.Add(new NpgsqlParameter { Value = json, NpgsqlDbType = NpgsqlDbType.Jsonb });
        parameters.Add(new NpgsqlParameter { Value = DateTimeOffset.UtcNow + _ttl });
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
