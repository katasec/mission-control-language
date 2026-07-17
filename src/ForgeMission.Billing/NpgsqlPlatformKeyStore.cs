using Npgsql;

namespace ForgeMission.Billing;

/// <summary>
/// Raw-Npgsql implementation of <see cref="IPlatformKeyStore"/> over <c>authbilling_db</c> (42.6). No
/// EF, so ForgeAPI stays an AOT target. <c>platform_keys</c> is a pure key→value lookup — point reads
/// on the request path, a single insert on issuance, one update on revocation.
/// </summary>
public sealed class NpgsqlPlatformKeyStore(NpgsqlDataSource dataSource) : IPlatformKeyStore
{
    public async Task SaveAsync(PlatformKey key, CancellationToken ct = default)
    {
        if (key.CreatedAt == default) key.CreatedAt = DateTimeOffset.UtcNow;

        await using var cmd = dataSource.CreateCommand("""
            INSERT INTO platform_keys (key_id, secret_hash, member_id, created_at, revoked_at)
            VALUES ($1, $2, $3, $4, $5)
            """);
        cmd.Parameters.Add(new NpgsqlParameter { Value = key.KeyId });
        cmd.Parameters.Add(new NpgsqlParameter { Value = key.SecretHash });
        cmd.Parameters.Add(new NpgsqlParameter { Value = key.MemberId });
        cmd.Parameters.Add(new NpgsqlParameter { Value = key.CreatedAt });
        cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)key.RevokedAt ?? DBNull.Value });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<PlatformKey?> ResolveByKeyIdAsync(string keyId, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand(
            "SELECT key_id, secret_hash, member_id, created_at, revoked_at FROM platform_keys WHERE key_id = $1");
        cmd.Parameters.Add(new NpgsqlParameter { Value = keyId });

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new PlatformKey
        {
            KeyId      = reader.GetString(0),
            SecretHash = reader.GetString(1),
            MemberId   = reader.GetGuid(2),
            CreatedAt  = reader.GetFieldValue<DateTimeOffset>(3),
            RevokedAt  = await reader.IsDBNullAsync(4, ct) ? null : reader.GetFieldValue<DateTimeOffset>(4),
        };
    }

    public async Task RevokeAsync(string keyId, CancellationToken ct = default)
    {
        // Idempotent: the WHERE guard makes an already-revoked or unknown key a no-op.
        await using var cmd = dataSource.CreateCommand(
            "UPDATE platform_keys SET revoked_at = $1 WHERE key_id = $2 AND revoked_at IS NULL");
        cmd.Parameters.Add(new NpgsqlParameter { Value = DateTimeOffset.UtcNow });
        cmd.Parameters.Add(new NpgsqlParameter { Value = keyId });
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
