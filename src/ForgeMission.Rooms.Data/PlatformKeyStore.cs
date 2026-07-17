using Microsoft.EntityFrameworkCore;

namespace ForgeMission.Rooms.Data;

/// <summary>
/// Persistence for <see cref="PlatformKey"/> (42.5). The store hides the backend: today it is
/// EF Core / Postgres, but <c>platform_keys</c> is a pure key→value lookup with no joins or
/// aggregation and is the first table earmarked to move to Azure Table Storage — swapping this
/// implementation is a one-line DI change (see docs/design/persistence.md). Callers (issuance,
/// the request-path lookup lib, /me, revocation) depend only on this interface.
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

public sealed class PlatformKeyStore(IDbContextFactory<RoomsDbContext> factory) : IPlatformKeyStore
{
    public async Task SaveAsync(PlatformKey key, CancellationToken ct = default)
    {
        if (key.CreatedAt == default) key.CreatedAt = DateTimeOffset.UtcNow;
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Set<PlatformKey>().Add(key);
        await db.SaveChangesAsync(ct);
    }

    public async Task<PlatformKey?> ResolveByKeyIdAsync(string keyId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Set<PlatformKey>().AsNoTracking()
            .FirstOrDefaultAsync(k => k.KeyId == keyId, ct);
    }

    public async Task RevokeAsync(string keyId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var key = await db.Set<PlatformKey>().FirstOrDefaultAsync(k => k.KeyId == keyId, ct);
        if (key is null || key.RevokedAt is not null) return;
        key.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
