using Npgsql;
using NpgsqlTypes;

namespace ForgeMission.Billing;

/// <summary>
/// Raw-Npgsql implementation of <see cref="ILedgerStore"/> over <c>authbilling_db</c> (42.6). No EF,
/// so it stays AOT-safe for ForgeAPI. Balance is <c>SUM(amount_micro_usd)</c>; <see cref="LedgerEntryKind"/>
/// is persisted as its lowercase name (matching the string the EF <c>rooms_db</c> version wrote).
/// </summary>
public sealed class NpgsqlLedgerStore(NpgsqlDataSource dataSource) : ILedgerStore
{
    public async Task<long> GetBalanceMicroUsdAsync(Guid memberId, CancellationToken ct = default)
    {
        // SUM over bigint widens to numeric in Postgres — cast back to bigint so the scalar is Int64.
        await using var cmd = dataSource.CreateCommand(
            "SELECT CAST(COALESCE(SUM(amount_micro_usd), 0) AS bigint) FROM ledger_entries WHERE member_id = $1");
        cmd.Parameters.Add(new NpgsqlParameter { Value = memberId });
        return (long)(await cmd.ExecuteScalarAsync(ct) ?? 0L);
    }

    public async Task AppendAsync(LedgerEntry entry, CancellationToken ct = default)
    {
        if (entry.Id == Guid.Empty) entry.Id = Guid.NewGuid();
        if (entry.CreatedAt == default) entry.CreatedAt = DateTimeOffset.UtcNow;

        await using var cmd = dataSource.CreateCommand("""
            INSERT INTO ledger_entries
                (id, member_id, amount_micro_usd, kind, description,
                 mission_ref, model, input_tokens, output_tokens, compute_seconds, created_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)
            """);
        var p = cmd.Parameters;
        p.Add(new NpgsqlParameter { Value = entry.Id });
        p.Add(new NpgsqlParameter { Value = entry.MemberId });
        p.Add(new NpgsqlParameter { Value = entry.AmountMicroUsd });
        p.Add(new NpgsqlParameter { Value = KindToDb(entry.Kind) });
        p.Add(Nullable(entry.Description));
        p.Add(Nullable(entry.MissionRef));
        p.Add(Nullable(entry.Model));
        p.Add(Nullable(entry.InputTokens));
        p.Add(Nullable(entry.OutputTokens));
        p.Add(Nullable(entry.ComputeSeconds));
        p.Add(new NpgsqlParameter { Value = entry.CreatedAt });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> HasEntryOfKindAsync(Guid memberId, LedgerEntryKind kind, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand(
            "SELECT EXISTS(SELECT 1 FROM ledger_entries WHERE member_id = $1 AND kind = $2)");
        cmd.Parameters.Add(new NpgsqlParameter { Value = memberId });
        cmd.Parameters.Add(new NpgsqlParameter { Value = KindToDb(kind) });
        return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
    }

    private static string KindToDb(LedgerEntryKind kind) => kind.ToString().ToLowerInvariant();

    // Positional params can't infer type from DBNull alone, so nullable columns carry an explicit type.
    private static NpgsqlParameter Nullable(string? value) =>
        new() { Value = (object?)value ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Varchar };
    private static NpgsqlParameter Nullable(long? value) =>
        new() { Value = (object?)value ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Bigint };
    private static NpgsqlParameter Nullable(double? value) =>
        new() { Value = (object?)value ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Double };
}
