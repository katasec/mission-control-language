namespace ForgeMission.Billing;

/// <summary>
/// Persistence for <see cref="PlatformKey"/> (42.5). The store hides the backend: ForgeUI backs it
/// with EF/Postgres over <c>rooms_db</c> today, and (42.6) ForgeAPI backs it with raw Npgsql over the
/// AOT-clean <c>authbilling_db</c> — <c>platform_keys</c> is a pure key→value lookup with no joins or
/// aggregation, so both are trivial. Callers (issuance, the request-path resolver, /me, revocation)
/// depend only on this interface.
/// </summary>
public interface IPlatformKeyStore
{
    /// <summary>Persist a newly minted key (issuance, 42.5 T4 ①).</summary>
    Task SaveAsync(PlatformKey key, CancellationToken ct = default);

    /// <summary>Load a key by its lookup id, or null if unknown (request-path resolution, ③).
    /// The caller verifies the presented secret against <see cref="PlatformKey.SecretHash"/> and
    /// checks <see cref="PlatformKey.RevokedAt"/>.</summary>
    Task<PlatformKey?> ResolveByKeyIdAsync(string keyId, CancellationToken ct = default);

    /// <summary>Mark a key revoked (idempotent — a no-op if already revoked or unknown). T6.</summary>
    Task RevokeAsync(string keyId, CancellationToken ct = default);
}
