using ForgeMission.Billing;
using ForgeMission.Rooms;
using ForgeMission.Rooms.Data;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeMission.Rooms.Tests;

/// <summary>
/// Request-path resolution (42.5 ③) against real Postgres: a valid key resolves to its member +
/// balance; malformed / unknown / wrong-secret / revoked keys resolve to null; and the cache serves
/// within the TTL but re-reads (picking up balance + revocation) once it expires.
/// </summary>
public sealed class PlatformKeyResolverTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Hmac = "resolver-test-hmac";

    private IWriteStore Writes => fixture.Services.GetRequiredService<IWriteStore>();
    private IPlatformKeyStore Keys => fixture.Services.GetRequiredService<IPlatformKeyStore>();
    private ILedgerStore Ledger => fixture.Services.GetRequiredService<ILedgerStore>();

    private PlatformKeyResolver NewResolver(PlatformKeyResolverOptions options) =>
        new(Keys, Ledger, options);

    private static PlatformKeyResolverOptions Options(Func<DateTimeOffset>? clock = null) => new()
    {
        HmacKey = Hmac,
        CacheTtl = TimeSpan.FromSeconds(30),
        Clock = clock ?? (() => DateTimeOffset.UtcNow),
    };

    private async Task<Guid> NewMemberAsync()
    {
        var m = await Writes.AddMemberAsync(new Member
        {
            Kind = MemberKind.Human,
            DisplayName = "Resolver Tester",
            Issuer = "dev",
            Subject = $"sub-{Guid.NewGuid():N}",
        });
        return m.Id;
    }

    private async Task<(string Token, Guid MemberId)> IssueKeyAsync(long grantMicroUsd)
    {
        var memberId = await NewMemberAsync();
        if (grantMicroUsd != 0)
            await Ledger.AppendAsync(new LedgerEntry { MemberId = memberId, AmountMicroUsd = grantMicroUsd, Kind = LedgerEntryKind.Grant });
        var minted = PlatformKeyMinting.Mint(Hmac);
        await Keys.SaveAsync(new PlatformKey { KeyId = minted.KeyId, SecretHash = minted.SecretHash, MemberId = memberId });
        return (minted.Token, memberId);
    }

    [Fact]
    public async Task Valid_key_resolves_to_member_and_balance()
    {
        var (token, memberId) = await IssueKeyAsync(5_000_000);

        var ctx = await NewResolver(Options()).ResolveAsync(token);

        Assert.NotNull(ctx);
        Assert.Equal(memberId, ctx!.MemberId);
        Assert.Equal(5_000_000, ctx.BalanceMicroUsd);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("garbage")]
    [InlineData("fg_live_deadbeef_deadbeef")] // well-formed but unknown key-id
    public async Task Malformed_or_unknown_keys_resolve_to_null(string? token)
    {
        Assert.Null(await NewResolver(Options()).ResolveAsync(token));
    }

    [Fact]
    public async Task Wrong_secret_resolves_to_null()
    {
        var (token, _) = await IssueKeyAsync(1_000_000);
        var tampered = token[..^4] + "0000"; // same key-id, wrong secret

        Assert.Null(await NewResolver(Options()).ResolveAsync(tampered));
    }

    [Fact]
    public async Task Revoked_key_resolves_to_null_after_ttl()
    {
        var (token, _) = await IssueKeyAsync(1_000_000);
        var now = DateTimeOffset.UtcNow;
        var resolver = NewResolver(Options(() => now));

        Assert.NotNull(await resolver.ResolveAsync(token)); // cached valid

        var keyId = PlatformKeyMinting.TryParse(token)!.Value.KeyId;
        await Keys.RevokeAsync(keyId);

        // Still cached as valid within the TTL...
        Assert.NotNull(await resolver.ResolveAsync(token));
        // ...rejected once the cache expires and the DB is re-read.
        now = now.AddSeconds(31);
        Assert.Null(await resolver.ResolveAsync(token));
    }

    [Fact]
    public async Task Balance_change_is_picked_up_after_ttl()
    {
        var (token, memberId) = await IssueKeyAsync(2_000_000);
        var now = DateTimeOffset.UtcNow;
        var resolver = NewResolver(Options(() => now));

        Assert.Equal(2_000_000, (await resolver.ResolveAsync(token))!.BalanceMicroUsd);

        await Ledger.AppendAsync(new LedgerEntry { MemberId = memberId, AmountMicroUsd = -500_000, Kind = LedgerEntryKind.Debit });

        Assert.Equal(2_000_000, (await resolver.ResolveAsync(token))!.BalanceMicroUsd); // still cached
        now = now.AddSeconds(31);
        Assert.Equal(1_500_000, (await resolver.ResolveAsync(token))!.BalanceMicroUsd); // re-read
    }
}
