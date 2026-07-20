using Npgsql;

namespace ForgeMission.Billing;

/// <summary>
/// Idempotent bootstrap for the <c>authbilling_db</c> schema (42.6 task 2). This database is the
/// billing bounded context's own store — <b>no EF, no migrations</b>: the two-table schema is created
/// with <c>CREATE TABLE IF NOT EXISTS</c> at host startup, cheap and safe to run every boot (incl.
/// prod). The tables mirror the columns the EF <c>rooms_db</c> versions had, minus the FK to
/// <c>members</c> — <c>member_id</c> is the only cross-context link and there is no cross-DB FK.
/// </summary>
public static class AuthBillingSchema
{
    private const string Sql = """
        CREATE TABLE IF NOT EXISTS ledger_entries (
            id                uuid                     PRIMARY KEY,
            member_id         uuid                     NOT NULL,
            amount_micro_usd  bigint                   NOT NULL,
            kind              varchar(16)              NOT NULL,
            description       varchar(512),
            mission_ref       varchar(128),
            model             varchar(128),
            input_tokens      bigint,
            output_tokens     bigint,
            compute_seconds   double precision,
            created_at        timestamptz              NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_ledger_entries_member_id ON ledger_entries (member_id);

        -- 42.6 task 5a (M7): idempotency key for the debit path. Added via ALTER, not just the
        -- CREATE TABLE above, because authbilling_db already exists live (task 2) with rows in it.
        ALTER TABLE ledger_entries ADD COLUMN IF NOT EXISTS client_token varchar(128);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_ledger_entries_client_token
            ON ledger_entries (client_token) WHERE client_token IS NOT NULL;

        CREATE TABLE IF NOT EXISTS platform_keys (
            key_id      varchar(64)   PRIMARY KEY,
            secret_hash varchar(128)  NOT NULL,
            member_id   uuid          NOT NULL,
            created_at  timestamptz   NOT NULL,
            revoked_at  timestamptz
        );
        CREATE INDEX IF NOT EXISTS ix_platform_keys_member_id ON platform_keys (member_id);
        """;

    /// <summary>Create the ledger + platform-key tables if absent. Idempotent.</summary>
    public static async Task EnsureCreatedAsync(NpgsqlDataSource dataSource, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand(Sql);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
