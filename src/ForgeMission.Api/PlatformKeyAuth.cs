using ForgeMission.Billing;

namespace ForgeMission.Api;

/// <summary>
/// Edge authentication for the hosted <c>/v1</c> endpoint (42.6 task 4). Every relayed request must
/// carry a platform key as a <c>Bearer</c> token; the filter resolves it to a
/// <see cref="PlatformKeyContext"/> (member + balance) via the shared <see cref="IPlatformKeyResolver"/>
/// and stashes it on <see cref="HttpContext.Items"/> for the routing (task 5) and billing (task 6)
/// wraps downstream. A missing, malformed, unknown, wrong-secret, or revoked key is a clean
/// <c>401</c> — the relay behind this filter never runs on an unauthenticated request.
/// </summary>
public sealed class PlatformKeyAuthFilter(IPlatformKeyResolver resolver) : IEndpointFilter
{
    private const string PrincipalItemKey = "forge.principal";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var http = ctx.HttpContext;
        var principal = await ResolvePrincipalAsync(http);
        if (principal is null)
            return Unauthorized();

        http.Items[PrincipalItemKey] = principal;
        return await next(ctx);
    }

    /// <summary>The caller resolved for the current request, or null before the auth filter has run
    /// (i.e. off the relay path). Task 5/6 read this to route + bill.</summary>
    public static PlatformKeyContext? Principal(HttpContext http) =>
        http.Items[PrincipalItemKey] as PlatformKeyContext;

    private Task<PlatformKeyContext?> ResolvePrincipalAsync(HttpContext http)
    {
        var token = BearerToken(http);
        return token is null
            ? Task.FromResult<PlatformKeyContext?>(null)
            : resolver.ResolveAsync(token, http.RequestAborted);
    }

    private static string? BearerToken(HttpContext http)
    {
        var header = http.Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : null;
    }

    private static IResult Unauthorized() =>
        Results.Json(
            ApiError.Auth("Invalid or missing platform key."),
            ApiJsonContext.Default.ApiError,
            statusCode: StatusCodes.Status401Unauthorized);
}
