using ForgeMission.Billing;
using Microsoft.EntityFrameworkCore;

namespace ForgeMission.Rooms.Data;

/// <summary>
/// EF/Postgres implementation of <see cref="IPlatformKeyStore"/> over <c>rooms_db</c> — the ForgeUI
/// host's key store. (42.6 adds a raw-Npgsql sibling on ForgeAPI over <c>authbilling_db</c>; both
/// satisfy the same abstraction in <c>ForgeMission.Billing</c>.) <c>platform_keys</c> is a pure
/// key→value lookup with no joins or aggregation.
/// </summary>
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
