using System.Collections.Concurrent;

namespace ForgeMission.Rooms.Data;

/// <summary>
/// Runtime execution context for a resolved platform key (42.5 ③): who the caller is and what they
/// can spend. Not a persisted principal — it is derived per request and cached briefly.
/// </summary>
public sealed record PlatformKeyContext(Guid MemberId, long BalanceMicroUsd);

public sealed class PlatformKeyResolverOptions
{
    /// <summary>Shared server key for the HMAC over the secret — must match the issuer's
    /// <c>PlatformKeys:HmacKey</c>, or every verify fails.</summary>
    public string HmacKey { get; init; } = "";

    /// <summary>How long a resolution is trusted before re-reading the DB. Revocation and balance
    /// changes propagate within this window (the accepted staleness bound; ~30–60 s).</summary>
    public TimeSpan CacheTtl { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Injectable clock (tests drive TTL expiry deterministically).</summary>
    public Func<DateTimeOffset> Clock { get; init; } = () => DateTimeOffset.UtcNow;
}

/// <summary>Resolve a presented <c>fg_live_…</c> token to its caller + balance, or null if it is
/// malformed, unknown, has the wrong secret, or is revoked.</summary>
public interface IPlatformKeyResolver
{
    Task<PlatformKeyContext?> ResolveAsync(string? presentedKey, CancellationToken ct = default);
}

/// <summary>
/// The request-path lookup lib (42.5 ③). Composes <see cref="IPlatformKeyStore"/> (key → member)
/// and <see cref="ILedgerStore"/> (member → balance) behind a short in-process cache. Runs in the
/// runner (data plane); reading Rooms Postgres directly avoids an RPC hop to the control plane.
///
/// A cache hit still verifies the presented secret against the cached hash — the cache holds the
/// hash, never the secret — so a stolen key-id without the secret never resolves.
/// </summary>
public sealed class PlatformKeyResolver(
    IPlatformKeyStore keys, ILedgerStore ledger, PlatformKeyResolverOptions options) : IPlatformKeyResolver
{
    private sealed record Entry(Guid MemberId, string SecretHash, bool Revoked, long BalanceMicroUsd, DateTimeOffset Expiry);

    private readonly ConcurrentDictionary<string, Entry> _cache = new();

    public async Task<PlatformKeyContext?> ResolveAsync(string? presentedKey, CancellationToken ct = default)
    {
        if (PlatformKeyMinting.TryParse(presentedKey) is not { } parsed)
            return null;
        var (keyId, secret) = parsed;

        var entry = await GetEntryAsync(keyId, ct);
        if (entry is null)                                                       return null; // unknown
        if (!PlatformKeyMinting.Verify(secret, options.HmacKey, entry.SecretHash)) return null; // wrong secret
        if (entry.Revoked)                                                        return null; // revoked

        return new PlatformKeyContext(entry.MemberId, entry.BalanceMicroUsd);
    }

    private async Task<Entry?> GetEntryAsync(string keyId, CancellationToken ct)
    {
        if (_cache.TryGetValue(keyId, out var cached) && cached.Expiry > options.Clock())
            return cached;

        var key = await keys.ResolveByKeyIdAsync(keyId, ct);
        if (key is null)
        {
            _cache.TryRemove(keyId, out _);
            return null;
        }

        var balance = await ledger.GetBalanceMicroUsdAsync(key.MemberId, ct);
        var entry = new Entry(key.MemberId, key.SecretHash, key.RevokedAt is not null, balance, options.Clock() + options.CacheTtl);
        _cache[keyId] = entry;
        return entry;
    }
}
