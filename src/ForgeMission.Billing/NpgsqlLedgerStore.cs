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
                 mission_ref, model, input_tokens, output_tokens, compute_seconds, client_token, created_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12)
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
        p.Add(Nullable(entry.ClientToken));
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

    public async Task<LedgerEntry?> FindByClientTokenAsync(string clientToken, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand("""
            SELECT id, member_id, amount_micro_usd, kind, description,
                   mission_ref, model, input_tokens, output_tokens, compute_seconds, client_token, created_at
            FROM ledger_entries WHERE client_token = $1
            """);
        cmd.Parameters.Add(new NpgsqlParameter { Value = clientToken });
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new LedgerEntry
        {
            Id             = reader.GetGuid(0),
            MemberId       = reader.GetGuid(1),
            AmountMicroUsd = reader.GetInt64(2),
            Kind           = KindFromDb(reader.GetString(3)),
            Description    = reader.IsDBNull(4) ? null : reader.GetString(4),
            MissionRef     = reader.IsDBNull(5) ? null : reader.GetString(5),
            Model          = reader.IsDBNull(6) ? null : reader.GetString(6),
            InputTokens    = reader.IsDBNull(7) ? null : reader.GetInt64(7),
            OutputTokens   = reader.IsDBNull(8) ? null : reader.GetInt64(8),
            ComputeSeconds = reader.IsDBNull(9) ? null : reader.GetDouble(9),
            ClientToken    = reader.IsDBNull(10) ? null : reader.GetString(10),
            CreatedAt      = reader.GetFieldValue<DateTimeOffset>(11),
        };
    }

    private static string KindToDb(LedgerEntryKind kind) => kind.ToString().ToLowerInvariant();
    private static LedgerEntryKind KindFromDb(string kind) => Enum.Parse<LedgerEntryKind>(kind, ignoreCase: true);

    // Positional params can't infer type from DBNull alone, so nullable columns carry an explicit type.
    private static NpgsqlParameter Nullable(string? value) =>
        new() { Value = (object?)value ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Varchar };
    private static NpgsqlParameter Nullable(long? value) =>
        new() { Value = (object?)value ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Bigint };
    private static NpgsqlParameter Nullable(double? value) =>
        new() { Value = (object?)value ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Double };
}
