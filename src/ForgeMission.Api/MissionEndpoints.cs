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
        app.MapPost("/api/UploadArtifact", UploadArtifactAsync).AddEndpointFilter<PlatformKeyAuthFilter>();
        app.MapPost("/api/GetArtifact", GetArtifactAsync).AddEndpointFilter<PlatformKeyAuthFilter>();
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
            ExecuteMissionResponse response;
            try
            {
                response = await svc.ExecuteAsync(msg, principal, ctx.RequestAborted);
            }
            catch
            {
                response = new ExecuteMissionResponse
                {
                    ResponseStatus = ResponseStatus.Fail(ErrorCode.RunFailed, "The mission run failed."),
                };
            }
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

    private static async Task UploadArtifactAsync(HttpContext ctx, IArtifactStore artifacts)
    {
        var principal = PlatformKeyAuthFilter.Principal(ctx)!;
        var message = UploadMessage(ctx);
        if (string.IsNullOrWhiteSpace(message.Name))
        {
            await WriteUploadJsonAsync(ctx,
                new UploadArtifactResponse
                {
                    ResponseStatus = ResponseStatus.Fail(ErrorCode.InvalidInput, "X-Forge-Artifact-Name is required."),
                },
                StatusCodes.Status400BadRequest);
            return;
        }

        ArtifactSaveResult saved;
        try
        {
            saved = await artifacts.SaveAsync(
                new ArtifactWriteRequest(
                    message.Name,
                    message.ContentType,
                    message.Sha256,
                    ArtifactRole.Input,
                    message.Size > 0 ? message.Size : null),
                ctx.Request.Body,
                principal,
                ctx.RequestAborted);
        }
        catch (ArtifactTooLargeException ex)
        {
            await WriteUploadJsonAsync(ctx,
                new UploadArtifactResponse
                {
                    ResponseStatus = ResponseStatus.Fail(ErrorCode.InvalidInput, ex.Message),
                },
                StatusCodes.Status400BadRequest);
            return;
        }

        if (!saved.Sha256Matched)
        {
            await WriteUploadJsonAsync(ctx,
                new UploadArtifactResponse
                {
                    ResponseStatus = ResponseStatus.Fail(ErrorCode.InvalidInput, "Uploaded bytes did not match X-Forge-Artifact-Sha256."),
                },
                StatusCodes.Status400BadRequest);
            return;
        }

        await WriteUploadJsonAsync(ctx,
            new UploadArtifactResponse { Artifact = saved.Artifact, ResponseStatus = ResponseStatus.Ok() },
            StatusCodes.Status200OK);
    }

    private static async Task GetArtifactAsync(
        HttpContext ctx, GetArtifact msg, IArtifactStore artifacts)
    {
        var principal = PlatformKeyAuthFilter.Principal(ctx)!;
        if (string.IsNullOrWhiteSpace(msg.ArtifactId))
        {
            await WriteStatusJsonAsync(ctx,
                new ResponseStatus { ErrorCode = ErrorCode.InvalidInput, Message = "ArtifactId is required." },
                StatusCodes.Status400BadRequest);
            return;
        }

        await using var read = await artifacts.OpenAsync(msg.ArtifactId, principal, ctx.RequestAborted);
        if (read is null)
        {
            await WriteStatusJsonAsync(ctx,
                ResponseStatus.Fail(ErrorCode.ArtifactNotFound, $"Artifact '{msg.ArtifactId}' was not found."),
                StatusCodes.Status404NotFound);
            return;
        }

        ctx.Response.StatusCode = StatusCodes.Status200OK;
        ctx.Response.ContentType = read.Artifact.ContentType;
        ctx.Response.ContentLength = read.Artifact.Size;
        ctx.Response.Headers.ContentDisposition = $"attachment; filename=\"{read.Artifact.Name}\"";
        ctx.Response.Headers["X-Forge-Artifact-Sha256"] = read.Artifact.Sha256;
        ctx.Response.Headers["X-Forge-Artifact-Size"] = read.Artifact.Size.ToString();
        await read.Content.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
        await artifacts.DeleteAsync(msg.ArtifactId, principal, ctx.RequestAborted);
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
        ErrorCode.MissionNotFound or ErrorCode.RunNotFound or ErrorCode.ArtifactNotFound => StatusCodes.Status404NotFound,
        ErrorCode.InsufficientCredit => StatusCodes.Status402PaymentRequired,
        ErrorCode.Unauthenticated => StatusCodes.Status401Unauthorized,
        ErrorCode.InvalidInput => StatusCodes.Status400BadRequest,
        ErrorCode.PolicyViolation => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError,
    };

    private static UploadArtifact UploadMessage(HttpContext ctx) => new()
    {
        Version = 1,
        ClientToken = Header(ctx, "X-Forge-Artifact-Client-Token"),
        Name = Header(ctx, "X-Forge-Artifact-Name"),
        ContentType = ctx.Request.ContentType ?? "application/octet-stream",
        Size = ctx.Request.ContentLength ?? 0,
        Sha256 = Header(ctx, "X-Forge-Artifact-Sha256"),
    };

    private static string Header(HttpContext ctx, string name) =>
        ctx.Request.Headers.TryGetValue(name, out var value) ? value.ToString() : "";

    private static async Task WriteUploadJsonAsync(HttpContext ctx, UploadArtifactResponse value, int statusCode)
    {
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json";
        var json = JsonSerializer.SerializeToUtf8Bytes(value, MessagesJsonContext.Default.UploadArtifactResponse);
        await ctx.Response.Body.WriteAsync(json, ctx.RequestAborted);
    }

    private static async Task WriteStatusJsonAsync(HttpContext ctx, ResponseStatus value, int statusCode)
    {
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json";
        var json = JsonSerializer.SerializeToUtf8Bytes(value, MessagesJsonContext.Default.ResponseStatus);
        await ctx.Response.Body.WriteAsync(json, ctx.RequestAborted);
    }
}
