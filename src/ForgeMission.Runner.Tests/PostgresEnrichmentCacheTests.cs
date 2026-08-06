using Npgsql;

namespace ForgeMission.Runner.Tests;

/// <summary>Real-Postgres coverage for the runner's shared re-entrancy store.</summary>
public sealed class PostgresEnrichmentCacheTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Set_then_get_round_trips_a_snapshot_across_data_sources()
    {
        await using var writerDataSource = new NpgsqlDataSourceBuilder(fixture.ConnectionString).Build();
        await EnrichmentCacheSchema.EnsureCreatedAsync(writerDataSource);
        await EnrichmentCacheSchema.EnsureCreatedAsync(writerDataSource); // bootstrap remains safe on every boot

        var snapshot = new Dictionary<string, string>
        {
            ["retrieval"] = "grounded context",
            ["guard"] = "allowed",
        };
        var writer = new PostgresEnrichmentCache(writerDataSource);
        await writer.SetAsync("prefix-hash", snapshot);

        await using var readerDataSource = new NpgsqlDataSourceBuilder(fixture.ConnectionString).Build();
        var reader = new PostgresEnrichmentCache(readerDataSource);
        var restored = await reader.GetAsync("prefix-hash");

        Assert.NotNull(restored);
        Assert.Equal(snapshot.Count, restored.Count);
        Assert.Equal("grounded context", restored["retrieval"]);
        Assert.Equal("allowed", restored["guard"]);
    }
}
