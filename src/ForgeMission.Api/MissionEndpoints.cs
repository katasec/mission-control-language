using System.Text.Json;
using ForgeMission.Billing;

namespace ForgeMission.Api;

/// <summary>
/// API A — mission invocation (42.6 task 5a). Each message is its own endpoint per the "Transport
/// mapping" section of the phase-42.6 spoke: <c>POST /api/{MessageName}</c>. The message is the
/// contract (M1) — this file is purely the HTTP adapter (route → principal → typed handler → JSON),
/// no business logic lives here.
/// </summary>
public static class MissionEndpoints
{
    public static void MapMissionEndpoints(this WebApplication app)
    {
        app.MapPost("/api/ExecuteMission", ExecuteMissionAsync).AddEndpointFilter<PlatformKeyAuthFilter>();
        app.MapPost("/api/SearchMissions", SearchMissionsAsync).AddEndpointFilter<PlatformKeyAuthFilter>();
        app.MapPost("/api/GetMission", GetMissionAsync).AddEndpointFilter<PlatformKeyAuthFilter>();
        app.MapPost("/api/GetAccount", GetAccountAsync).AddEndpointFilter<PlatformKeyAuthFilter>();
        app.MapPost("/api/GetRun", GetRunAsync).AddEndpointFilter<PlatformKeyAuthFilter>();
    }

    private static async Task ExecuteMissionAsync(
        HttpContext ctx, ExecuteMission msg, MissionExecutionService svc)
    {
        // The auth filter already gated this request — Principal(ctx) is always non-null here.
        var principal = PlatformKeyAuthFilter.Principal(ctx)!;

        if (!msg.Stream)
        {
            var response = await svc.ExecuteAsync(msg, principal, ctx.RequestAborted);
            ctx.Response.StatusCode = HttpStatus(response.ResponseStatus);
            ctx.Response.ContentType = "application/json";
            var json = JsonSerializer.SerializeToUtf8Bytes(response, MessagesJsonContext.Default.ExecuteMissionResponse);
            await ctx.Response.Body.WriteAsync(json, ctx.RequestAborted);
            return;
        }

        // M10: streaming is a sequence of messages; NDJSON framing, same as the runner's own
        // /run/stream — one MissionRunEvent per line, flushed as it happens.
        ctx.Response.ContentType = "application/x-ndjson";
        var newline = "\n"u8.ToArray();
        await foreach (var evt in svc.ExecuteStreamAsync(msg, principal, ctx.RequestAborted))
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(evt, MessagesJsonContext.Default.MissionRunEvent);
            await ctx.Response.Body.WriteAsync(json, ctx.RequestAborted);
            await ctx.Response.Body.WriteAsync(newline, ctx.RequestAborted);
            await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
        }
    }

    private static async Task<SearchMissionsResponse> SearchMissionsAsync(
        SearchMissions msg, IMissionCatalog catalog, CancellationToken ct)
    {
        var entries = await catalog.SearchAsync(msg.Query, msg.Publisher, ct);
        return new SearchMissionsResponse
        {
            Results = entries.Select(ToSummary).ToList(),
            ResponseStatus = ResponseStatus.Ok(),
        };
    }

    private static async Task<GetMissionResponse> GetMissionAsync(
        GetMission msg, IMissionCatalog catalog, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(msg.Mission))
            return new GetMissionResponse { ResponseStatus = ResponseStatus.Fail(ErrorCode.InvalidInput, "Mission is required.") };

        var handle = MissionHandle.Parse(msg.Mission);
        var entry = await catalog.ResolveAsync(handle, msg.MissionVersion, ct);
        if (entry is null)
            return new GetMissionResponse
            {
                ResponseStatus = ResponseStatus.Fail(ErrorCode.MissionNotFound, $"Mission '{msg.Mission}' was not found."),
            };

        var summary = ToSummary(entry);
        return new GetMissionResponse
        {
            Mission = summary.Mission,
            Description = summary.Description,
            Publisher = summary.Publisher,
            Version = summary.Version,
            Verified = summary.Verified,
            ResponseStatus = ResponseStatus.Ok(),
        };
    }

    private static async Task<GetAccountResponse> GetAccountAsync(
        HttpContext ctx, GetAccount msg, BillingService billing, CancellationToken ct)
    {
        var principal = PlatformKeyAuthFilter.Principal(ctx)!;
        var balance = await billing.GetBalanceMicroUsdAsync(principal.MemberId, ct);
        return new GetAccountResponse
        {
            MemberId = principal.MemberId.ToString(),
            Email = null, // decided null for now — see the DTO's own doc comment
            BalanceMicroUsd = balance,
            ResponseStatus = ResponseStatus.Ok(),
        };
    }

    private static async Task<GetRunResponse> GetRunAsync(GetRun msg, IRunStore runStore, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(msg.RunId))
            return new GetRunResponse { ResponseStatus = ResponseStatus.Fail(ErrorCode.InvalidInput, "RunId is required.") };

        var result = await runStore.TryGetAsync(msg.RunId, ct);
        if (result is null)
            return new GetRunResponse
            {
                RunId = msg.RunId,
                ResponseStatus = ResponseStatus.Fail(ErrorCode.RunNotFound, $"Run '{msg.RunId}' was not found."),
            };

        return new GetRunResponse
        {
            RunId = msg.RunId,
            Status = RunStatus.Completed,
            Result = result,
            ResponseStatus = ResponseStatus.Ok(),
        };
    }

    private static MissionSummary ToSummary(CatalogEntry entry) => new()
    {
        Mission = entry.Handle,
        Description = entry.Description,
        Publisher = entry.Publisher,
        Version = entry.Version,
        Verified = entry.Verified,
    };

    /// <summary>HTTP-status projection of <see cref="ResponseStatus"/> (a convenience — the message
    /// is authoritative per M4).</summary>
    private static int HttpStatus(ResponseStatus status) => status.ErrorCode switch
    {
        null or "" => StatusCodes.Status200OK,
        ErrorCode.MissionNotFound or ErrorCode.RunNotFound => StatusCodes.Status404NotFound,
        ErrorCode.InsufficientCredit => StatusCodes.Status402PaymentRequired,
        ErrorCode.Unauthenticated => StatusCodes.Status401Unauthorized,
        ErrorCode.InvalidInput => StatusCodes.Status400BadRequest,
        ErrorCode.PolicyViolation => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError,
    };
}
