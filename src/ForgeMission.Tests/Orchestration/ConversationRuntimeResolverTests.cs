using ForgeMission.Orchestration;
using Microsoft.Extensions.Configuration;

namespace ForgeMission.Tests.Orchestration;

// The durable Conversation Runtime endpoint contract: the existing ConversationRuntime:BaseUrl
// override wins when it is usable, one local development default otherwise, and an override that
// could never serve as a client BaseAddress fails at boot naming its own configuration key.
public sealed class ConversationRuntimeResolverTests
{
    [Fact]
    public void Resolve_NoConfiguration_UsesTheLocalDefault()
    {
        var endpoint = ConversationRuntimeResolver.Resolve(new ConfigurationBuilder().Build());

        Assert.Equal("http://127.0.0.1:18080/", endpoint.BaseUrl);
        Assert.Equal(ConversationRuntimeResolver.DefaultLocalBaseUrl, endpoint.BaseUrl);
        Assert.True(endpoint.IsLocalDefault);
    }

    [Fact]
    public void Resolve_WhitespaceOverride_UsesTheLocalDefault()
    {
        var endpoint = ConversationRuntimeResolver.Resolve(Configured("   "));

        Assert.Equal(ConversationRuntimeResolver.DefaultLocalBaseUrl, endpoint.BaseUrl);
        Assert.True(endpoint.IsLocalDefault);
    }

    [Fact]
    public void Resolve_ConfiguredOverride_WinsAndIsNotTheLocalDefault()
    {
        var endpoint = ConversationRuntimeResolver.Resolve(Configured("https://durable.forge.example/"));

        Assert.Equal("https://durable.forge.example/", endpoint.BaseUrl);
        Assert.False(endpoint.IsLocalDefault);
    }

    [Theory]
    [InlineData("http://127.0.0.1:9000", "http://127.0.0.1:9000/")]
    [InlineData("https://durable.forge.example/conversations", "https://durable.forge.example/conversations/")]
    public void Resolve_ConfiguredOverride_NormalizesTheTrailingSlash(string configured, string expected)
    {
        Assert.Equal(expected, ConversationRuntimeResolver.Resolve(Configured(configured)).BaseUrl);
    }

    [Theory]
    [InlineData("conversations")]
    [InlineData("/conversations")]
    [InlineData("ftp://durable.forge.example/")]
    [InlineData("not a url")]
    public void Resolve_UnusableOverride_ThrowsNamingTheConfigurationKey(string configured)
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => ConversationRuntimeResolver.Resolve(Configured(configured)));

        Assert.Contains(ConversationRuntimeResolver.ConfigurationKey, error.Message);
        Assert.Contains(configured, error.Message);
    }

    private static IConfiguration Configured(string baseUrl) => new ConfigurationBuilder()
        .AddInMemoryCollection([new(ConversationRuntimeResolver.ConfigurationKey, baseUrl)])
        .Build();
}
