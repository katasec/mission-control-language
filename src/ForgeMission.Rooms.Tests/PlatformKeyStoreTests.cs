using ForgeMission.Billing;
using ForgeMission.Rooms;
using ForgeMission.Rooms.Data;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeMission.Rooms.Tests;

/// <summary>
/// <see cref="IPlatformKeyStore"/> against real Postgres (42.5 ②): save then resolve by key-id,
/// unknown ids resolve to null, and revocation is durable + idempotent. Also exercises the
/// AddPlatformKeys migration (the fixture migrates before these run).
/// </summary>
public sealed class PlatformKeyStoreTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private IWriteStore Writes => fixture.Services.GetRequiredService<IWriteStore>();
    private IPlatformKeyStore Keys => fixture.Services.GetRequiredService<IPlatformKeyStore>();

    private async Task<Guid> NewMemberAsync()
    {
        var m = await Writes.AddMemberAsync(new Member
        {
            Kind = MemberKind.Human,
            DisplayName = "Key Tester",
            Issuer = "dev",
            Subject = $"sub-{Guid.NewGuid():N}",
        });
        return m.Id;
    }

    [Fact]
    public async Task Save_then_resolve_returns_the_key()
    {
        var memberId = await NewMemberAsync();
        var minted = PlatformKeyMinting.Mint("hmac");
        await Keys.SaveAsync(new PlatformKey { KeyId = minted.KeyId, SecretHash = minted.SecretHash, MemberId = memberId });

        var resolved = await Keys.ResolveByKeyIdAsync(minted.KeyId);

        Assert.NotNull(resolved);
        Assert.Equal(memberId, resolved!.MemberId);
        Assert.Equal(minted.SecretHash, resolved.SecretHash);
        Assert.Null(resolved.RevokedAt);
    }

    [Fact]
    public async Task Resolve_unknown_keyid_is_null()
    {
        Assert.Null(await Keys.ResolveByKeyIdAsync("does-not-exist"));
    }

    [Fact]
    public async Task Revoke_is_durable_and_idempotent()
    {
        var memberId = await NewMemberAsync();
        var minted = PlatformKeyMinting.Mint("hmac");
        await Keys.SaveAsync(new PlatformKey { KeyId = minted.KeyId, SecretHash = minted.SecretHash, MemberId = memberId });

        await Keys.RevokeAsync(minted.KeyId);
        var afterFirst = await Keys.ResolveByKeyIdAsync(minted.KeyId);
        Assert.NotNull(afterFirst!.RevokedAt);

        // Second revoke keeps the original timestamp (idempotent no-op).
        var stamp = afterFirst.RevokedAt;
        await Keys.RevokeAsync(minted.KeyId);
        var afterSecond = await Keys.ResolveByKeyIdAsync(minted.KeyId);
        Assert.Equal(stamp, afterSecond!.RevokedAt);
    }

    [Fact]
    public async Task Revoke_unknown_keyid_is_a_noop()
    {
        await Keys.RevokeAsync("nope"); // must not throw
    }
}
