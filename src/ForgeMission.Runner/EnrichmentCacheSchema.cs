using Npgsql;

namespace ForgeMission.Runner;

/// <summary>
/// Idempotent bootstrap for the runner-owned enrichment-cache store. It is intentionally a
/// separate database from <c>authbilling_db</c>: the runner executes mission code and must never
/// receive identity or billing credentials.
/// </summary>
public static class EnrichmentCacheSchema
{
    private const string Sql = """
        CREATE TABLE IF NOT EXISTS enrichment_cache (
            prefix_hash varchar(128) PRIMARY KEY,
            snapshot    jsonb        NOT NULL,
            expires_at  timestamptz   NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_enrichment_cache_expires_at
            ON enrichment_cache (expires_at);
        """;

    /// <summary>Create the enrichment-cache table if absent. Idempotent.</summary>
    public static async Task EnsureCreatedAsync(NpgsqlDataSource dataSource, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand(Sql);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
