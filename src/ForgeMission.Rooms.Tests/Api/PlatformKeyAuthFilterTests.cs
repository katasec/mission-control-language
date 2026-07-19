using ForgeMission.Api;
using ForgeMission.Billing;
using Microsoft.AspNetCore.Http;

namespace ForgeMission.Rooms.Tests;

/// <summary>
/// The ForgeAPI edge auth filter (42.6 task 4), in isolation from the DB: a valid Bearer platform key
/// resolves + passes through to the relay with the principal stashed; a missing or unresolvable key
/// short-circuits to a 401 and the relay never runs. Resolution itself (HMAC/revocation/cache) is
/// covered against real Postgres by <see cref="PlatformKeyResolverTests"/>.
/// </summary>
public sealed class PlatformKeyAuthFilterTests
{
    [Fact]
    public async Task Valid_key_passes_through_and_stashes_principal()
    {
        var principal = new PlatformKeyContext(Guid.NewGuid(), 5_000_000);
        var resolver = new StubResolver(_ => principal);
        var (ctx, next) = Invocation("Bearer fg_live_abc_def");

        var result = await new PlatformKeyAuthFilter(resolver).InvokeAsync(ctx, next.Delegate);

        Assert.True(next.WasCalled);
        Assert.Equal("relay-ran", result);
        Assert.Same(principal, PlatformKeyAuthFilter.Principal(ctx.HttpContext));
        Assert.Equal("fg_live_abc_def", resolver.LastToken);
    }

    [Fact]
    public async Task Unresolvable_key_short_circuits_401()
    {
        var resolver = new StubResolver(_ => null); // unknown / wrong-secret / revoked
        var (ctx, next) = Invocation("Bearer fg_live_bad_key");

        var result = await new PlatformKeyAuthFilter(resolver).InvokeAsync(ctx, next.Delegate);

        Assert.False(next.WasCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCodeOf(result));
        Assert.Null(PlatformKeyAuthFilter.Principal(ctx.HttpContext));
    }

    [Fact]
    public async Task Missing_header_is_401_without_touching_the_resolver()
    {
        var resolver = new StubResolver(_ => throw new Xunit.Sdk.XunitException("resolver must not be called"));
        var (ctx, next) = Invocation(authorization: null);

        var result = await new PlatformKeyAuthFilter(resolver).InvokeAsync(ctx, next.Delegate);

        Assert.False(next.WasCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCodeOf(result));
        Assert.Null(resolver.LastToken);
    }

    [Fact]
    public async Task Non_bearer_scheme_is_401()
    {
        var resolver = new StubResolver(_ => throw new Xunit.Sdk.XunitException("resolver must not be called"));
        var (ctx, next) = Invocation("Basic dXNlcjpwYXNz");

        var result = await new PlatformKeyAuthFilter(resolver).InvokeAsync(ctx, next.Delegate);

        Assert.False(next.WasCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCodeOf(result));
    }

    // --- harness ------------------------------------------------------------------------------

    private static (EndpointFilterInvocationContext Ctx, NextSpy Next) Invocation(string? authorization)
    {
        var http = new DefaultHttpContext();
        if (authorization is not null)
            http.Request.Headers.Authorization = authorization;
        var ctx = EndpointFilterInvocationContext.Create(http);
        return (ctx, new NextSpy());
    }

    private static int StatusCodeOf(object? result) =>
        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode ?? 0;

    private sealed class NextSpy
    {
        public bool WasCalled { get; private set; }

        public ValueTask<object?> Delegate(EndpointFilterInvocationContext _)
        {
            WasCalled = true;
            return ValueTask.FromResult<object?>("relay-ran");
        }
    }

    private sealed class StubResolver(Func<string?, PlatformKeyContext?> resolve) : IPlatformKeyResolver
    {
        public string? LastToken { get; private set; }

        public Task<PlatformKeyContext?> ResolveAsync(string? presentedKey, CancellationToken ct = default)
        {
            LastToken = presentedKey;
            return Task.FromResult(resolve(presentedKey));
        }
    }
}
