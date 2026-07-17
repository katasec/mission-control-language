using ForgeMission.Rooms.Data;

namespace ForgeMission.Rooms.Tests;

/// <summary>
/// Pure format/hash guarantees for platform keys (42.5): the token round-trips through
/// <see cref="PlatformKeyMinting.TryParse"/>, the stored hash verifies only the right secret,
/// and malformed tokens are rejected rather than mis-parsed.
/// </summary>
public sealed class PlatformKeyMintingTests
{
    private const string Hmac = "test-hmac-key";

    [Fact]
    public void Mint_produces_a_prefixed_token_that_parses_back_to_its_keyid()
    {
        var minted = PlatformKeyMinting.Mint(Hmac);

        Assert.StartsWith("fg_live_", minted.Token);
        var parsed = PlatformKeyMinting.TryParse(minted.Token);
        Assert.NotNull(parsed);
        Assert.Equal(minted.KeyId, parsed!.Value.KeyId);
    }

    [Fact]
    public void Verify_accepts_the_real_secret_and_rejects_others()
    {
        var minted = PlatformKeyMinting.Mint(Hmac);
        var secret = PlatformKeyMinting.TryParse(minted.Token)!.Value.Secret;

        Assert.True(PlatformKeyMinting.Verify(secret, Hmac, minted.SecretHash));
        Assert.False(PlatformKeyMinting.Verify(secret + "0", Hmac, minted.SecretHash));
        Assert.False(PlatformKeyMinting.Verify(secret, "wrong-hmac-key", minted.SecretHash));
    }

    [Fact]
    public void Mint_never_repeats_a_keyid_or_secret()
    {
        var a = PlatformKeyMinting.Mint(Hmac);
        var b = PlatformKeyMinting.Mint(Hmac);

        Assert.NotEqual(a.KeyId, b.KeyId);
        Assert.NotEqual(a.Token, b.Token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-forge-key")]
    [InlineData("fg_live_")]          // prefix only
    [InlineData("fg_live_abc")]       // no secret delimiter
    [InlineData("fg_live_abc_")]      // empty secret
    public void TryParse_rejects_malformed_tokens(string? token)
    {
        Assert.Null(PlatformKeyMinting.TryParse(token));
    }

    [Fact]
    public void Hash_is_hex_and_never_the_plaintext_secret()
    {
        var minted = PlatformKeyMinting.Mint(Hmac);
        var secret = PlatformKeyMinting.TryParse(minted.Token)!.Value.Secret;

        Assert.NotEqual(secret, minted.SecretHash);
        Assert.Matches("^[0-9a-f]{64}$", minted.SecretHash); // HMAC-SHA256 hex
    }
}
