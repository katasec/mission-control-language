using System.Text.Json;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.Conversations.Contracts;
using Orleans;

namespace ForgeMission.ConversationHost.Api;

/// <summary>
/// The Task 6 additive Forge-native Conversation message contract and its first (HTTP/SSE)
/// projection. The <c>Handle*Async</c> methods below are the transport-neutral message handlers: each
/// takes only a Contracts request message and <see cref="IGrainFactory"/>, contains no HTTP type,
/// and is directly callable by any adapter (including tests, with no HTTP meaning at all) or a
/// future gRPC/broker/in-process caller. The route delegates registered in
/// <see cref="MapConversationApi"/> are the thin HTTP projection: they bind route/query values into
/// the matching message, reject an HTTP-shape-only problem (malformed route GUID, non-numeric
/// <c>after</c>, a route/body <c>ConversationId</c> disagreement) before ever calling a handler, and
/// map the handler's typed outcome to an HTTP status/header. Neither layer passes
/// <c>HttpContext</c>/<c>IResult</c> into a grain, and no Contracts type, grain method, or
/// persistence interface exposes an HTTP type.
/// </summary>
/// <summary>Distinguishes a successful query from an expected non-exceptional outcome. Shared by
/// both query message handlers below — mirrors <c>ConversationCommandOutcome</c>'s shape for
/// mutations, but queries have no <c>Conflict</c> case.</summary>
public enum ConversationQueryOutcome
{
    Found,
    NotFound,
    Invalid,
}

/// <summary>Result of <see cref="ConversationApiEndpoints.HandleGetConversationAsync"/>.
/// <see cref="Response"/> is non-null only when <see cref="Outcome"/> is
/// <see cref="ConversationQueryOutcome.Found"/>; <see cref="Reason"/> is non-null otherwise.</summary>
public sealed record GetConversationOutcomeResult(
    ConversationQueryOutcome Outcome, GetConversationResponse? Response, string? Reason);

/// <summary>Result of <see cref="ConversationApiEndpoints.HandleReadConversationEventsAsync"/>.
/// <see cref="Events"/> is non-null only when <see cref="Outcome"/> is
/// <see cref="ConversationQueryOutcome.Found"/>; <see cref="Reason"/> is non-null otherwise.</summary>
public sealed record ReadConversationEventsOutcomeResult(
    ConversationQueryOutcome Outcome, ConversationEvent[]? Events, string? Reason);

public static class ConversationApiEndpoints
{
    private const string DevTenantId = "dev";
    private const string SupportedMissionRef = "Janus";

    public static void MapConversationApi(this WebApplication app)
    {
        app.MapPost("/conversations", StartConversationAsync);
        app.MapPost("/conversations/{conversationId}/commands", SubmitCommandAsync);
        app.MapPost("/conversations/{conversationId}/tool-results", SubmitToolResultAsync);
        app.MapGet("/conversations/{conversationId}", GetConversationAsync);
        app.MapGet("/conversations/{conversationId}/events", StreamEventsAsync);
    }

    // ═══════════════════════════ Transport-neutral message handlers ═══════════════════════════
    // No HTTP type appears in this section. Each handler validates its own message's fields (a
    // message-level concern, not an HTTP one) and returns a typed outcome the caller — HTTP today,
    // potentially something else later — maps to its own transport's status/error shape.

    public static async Task<ConversationCommandOutcomeResult> HandleStartConversationAsync(
        StartConversationRequest request, IGrainFactory grainFactory)
    {
        if (request.CommandId == Guid.Empty || string.IsNullOrEmpty(request.MissionRef) ||
            string.IsNullOrEmpty(request.Goal) || request.Capabilities is null)
            return Invalid("commandId, missionRef, and goal are required, and capabilities must be a non-null array.");

        if (request.MissionRef != SupportedMissionRef)
            return Invalid($"Unsupported missionRef '{request.MissionRef}'; only '{SupportedMissionRef}' is accepted.");

        // Deterministic, not random: an exact retry of this same CommandId lands on the same
        // conversation/run and reaches AcceptCommandAsync's own duplicate-acceptance path.
        var conversationId = ConversationDeterministicIds.Conversation(request.CommandId);
        var runId = ConversationDeterministicIds.InitialRun(request.CommandId);
        var address = new ConversationAddress(DevTenantId, conversationId);

        var command = new ConversationCommand(
            request.CommandId, conversationId, runId, ConversationCommandKind.StartMission,
            request.MissionRef, request.Goal, request.Capabilities, null);
        var commandJson = JsonSerializer.Serialize(command, ConversationContractsJsonContext.Default.ConversationCommand);

        var grain = grainFactory.GetGrain<IConversationGrain>(address.PartitionKey);
        return await grain.AcceptCommandAsync(new ConversationCommandInput(commandJson));
    }

    public static async Task<ConversationCommandOutcomeResult> HandleSubmitCommandAsync(
        SubmitConversationCommandRequest request, IGrainFactory grainFactory)
    {
        if (request.ConversationId == Guid.Empty || request.CommandId == Guid.Empty || string.IsNullOrEmpty(request.Text))
            return Invalid("conversationId, commandId, and text are required.");

        var address = new ConversationAddress(DevTenantId, request.ConversationId);
        var grain = grainFactory.GetGrain<IConversationGrain>(address.PartitionKey);

        if (await TryGetExistingSnapshotAsync(grain) is null)
            return new ConversationCommandOutcomeResult(ConversationCommandOutcome.NotFound, null, "Conversation not found.");

        return await grain.AcceptFollowupCommandAsync(new ConversationFollowupCommandInput(request.CommandId, request.Text));
    }

    public static async Task<ConversationCommandOutcomeResult> HandleSubmitToolResultAsync(
        SubmitToolResultRequest request, IGrainFactory grainFactory)
    {
        if (request.ConversationId == Guid.Empty || request.CommandId == Guid.Empty ||
            request.ToolRequestId == Guid.Empty || request.Content is null)
            return Invalid("conversationId, commandId, toolRequestId, and non-null content are required.");

        var address = new ConversationAddress(DevTenantId, request.ConversationId);
        var grain = grainFactory.GetGrain<IConversationGrain>(address.PartitionKey);

        if (await TryGetExistingSnapshotAsync(grain) is null)
            return new ConversationCommandOutcomeResult(ConversationCommandOutcome.NotFound, null, "Conversation not found.");

        return await grain.AcceptToolResultAsync(
            new ConversationToolResultInput(request.CommandId, request.ToolRequestId, request.Content, request.IsError));
    }

    public static async Task<GetConversationOutcomeResult> HandleGetConversationAsync(
        GetConversationRequest request, IGrainFactory grainFactory)
    {
        if (request.ConversationId == Guid.Empty)
            return new GetConversationOutcomeResult(ConversationQueryOutcome.Invalid, null, "conversationId is required.");

        var address = new ConversationAddress(DevTenantId, request.ConversationId);
        var grain = grainFactory.GetGrain<IConversationGrain>(address.PartitionKey);

        var snapshot = await TryGetExistingSnapshotAsync(grain);
        return snapshot is null
            ? new GetConversationOutcomeResult(ConversationQueryOutcome.NotFound, null, "Conversation not found.")
            : new GetConversationOutcomeResult(ConversationQueryOutcome.Found, new GetConversationResponse(snapshot), null);
    }

    /// <summary>Validates the message and yields its real, ordered, currently-durable
    /// <see cref="ConversationEvent"/> sequence (via the grain's own <c>ReadAfterAsync</c> — never
    /// <c>IConversationEventStore</c> directly) — not a boolean existence flag. This is the durable
    /// "what is available now" query; the SSE route separately layers live/reconnect framing on
    /// top of it via <see cref="ConversationSseWriter"/>, which is the one adapter-owned exception
    /// allowed to also read the store directly (for its own subscribe-ordered replay).</summary>
    public static async Task<ReadConversationEventsOutcomeResult> HandleReadConversationEventsAsync(
        ReadConversationEventsRequest request, IGrainFactory grainFactory)
    {
        if (request.ConversationId == Guid.Empty || request.After < 0)
            return new ReadConversationEventsOutcomeResult(
                ConversationQueryOutcome.Invalid, null, "conversationId is required and after must be non-negative.");

        var address = new ConversationAddress(DevTenantId, request.ConversationId);
        var grain = grainFactory.GetGrain<IConversationGrain>(address.PartitionKey);

        if (await TryGetExistingSnapshotAsync(grain) is null)
            return new ReadConversationEventsOutcomeResult(ConversationQueryOutcome.NotFound, null, "Conversation not found.");

        var batch = await grain.ReadAfterAsync(request.After);
        var events = batch.EventJson
            .Select(json => JsonSerializer.Deserialize(json, ConversationContractsJsonContext.Default.ConversationEvent)!)
            .ToArray();
        return new ReadConversationEventsOutcomeResult(ConversationQueryOutcome.Found, events, null);
    }

    // ══════════════════════════════ HTTP route delegates (thin) ═══════════════════════════════

    private static async Task<IResult> StartConversationAsync(StartConversationRequest request, IGrainFactory grainFactory)
    {
        var result = await HandleStartConversationAsync(request, grainFactory);
        return result.Outcome switch
        {
            ConversationCommandOutcome.Accepted => Results.Created(
                $"/conversations/{result.Acceptance!.ConversationId}",
                new StartConversationResponse(
                    result.Acceptance.ConversationId, result.Acceptance.RunId, result.Acceptance.AcceptedSequence, result.Acceptance.Status)),
            ConversationCommandOutcome.Invalid => BadRequest(result.Reason),
            ConversationCommandOutcome.Conflict => Conflict(result.Reason),
            _ => throw new InvalidOperationException($"Unhandled {nameof(ConversationCommandOutcome)} '{result.Outcome}' for start."),
        };
    }

    private static async Task<IResult> SubmitCommandAsync(
        string conversationId, SubmitConversationCommandRequest request, IGrainFactory grainFactory)
    {
        if (!TryParseRouteId(conversationId, out var routeId))
            return BadRequest("conversationId must be a valid, non-empty GUID.");
        if (request.ConversationId != routeId)
            return BadRequest("Route conversationId and request body ConversationId must agree.");

        var result = await HandleSubmitCommandAsync(request, grainFactory);
        return result.Outcome switch
        {
            ConversationCommandOutcome.Accepted => AcceptedWithHeaders(
                new SubmitConversationCommandResponse(
                    result.Acceptance!.ConversationId, result.Acceptance.RunId, result.Acceptance.AcceptedSequence, result.Acceptance.Status),
                $"/conversations/{result.Acceptance.ConversationId}"),
            ConversationCommandOutcome.Invalid => BadRequest(result.Reason),
            ConversationCommandOutcome.NotFound => NotFound(),
            ConversationCommandOutcome.Conflict => Conflict(result.Reason),
            _ => throw new InvalidOperationException($"Unhandled {nameof(ConversationCommandOutcome)} '{result.Outcome}' for follow-up."),
        };
    }

    private static async Task<IResult> SubmitToolResultAsync(
        string conversationId, SubmitToolResultRequest request, IGrainFactory grainFactory)
    {
        if (!TryParseRouteId(conversationId, out var routeId))
            return BadRequest("conversationId must be a valid, non-empty GUID.");
        if (request.ConversationId != routeId)
            return BadRequest("Route conversationId and request body ConversationId must agree.");

        var result = await HandleSubmitToolResultAsync(request, grainFactory);
        return result.Outcome switch
        {
            ConversationCommandOutcome.Accepted => AcceptedWithHeaders(
                new SubmitToolResultResponse(
                    result.Acceptance!.ConversationId, result.Acceptance.RunId, result.Acceptance.AcceptedSequence, result.Acceptance.Status),
                $"/conversations/{result.Acceptance.ConversationId}"),
            ConversationCommandOutcome.Invalid => BadRequest(result.Reason),
            ConversationCommandOutcome.NotFound => NotFound(),
            ConversationCommandOutcome.Conflict => Conflict(result.Reason),
            _ => throw new InvalidOperationException($"Unhandled {nameof(ConversationCommandOutcome)} '{result.Outcome}' for tool-result."),
        };
    }

    private static async Task<IResult> GetConversationAsync(string conversationId, IGrainFactory grainFactory)
    {
        if (!TryParseRouteId(conversationId, out var id))
            return BadRequest("conversationId must be a valid, non-empty GUID.");

        var result = await HandleGetConversationAsync(new GetConversationRequest(id), grainFactory);
        return result.Outcome switch
        {
            ConversationQueryOutcome.Found => Results.Ok(result.Response),
            ConversationQueryOutcome.NotFound => NotFound(),
            ConversationQueryOutcome.Invalid => BadRequest(result.Reason),
            _ => throw new InvalidOperationException($"Unhandled {nameof(ConversationQueryOutcome)} '{result.Outcome}' for get."),
        };
    }

    private static async Task<IResult> StreamEventsAsync(
        string conversationId, string? after, IGrainFactory grainFactory, ConversationSseWriter sseWriter,
        HttpResponse response, CancellationToken ct)
    {
        if (!TryParseRouteId(conversationId, out var id))
            return BadRequest("conversationId must be a valid, non-empty GUID.");

        long afterSequence = 0;
        if (!string.IsNullOrEmpty(after) && (!long.TryParse(after, out afterSequence) || afterSequence < 0))
            return BadRequest("after must be a non-negative integer.");

        // Validates the message and proves the conversation exists via the SAME real query a
        // non-HTTP caller would use. ConversationSseWriter then independently replays/subscribes —
        // it alone also needs the append-race-closing SECOND durable read and the live tail, which
        // this bounded, one-shot query does not attempt to provide.
        var queryResult = await HandleReadConversationEventsAsync(new ReadConversationEventsRequest(id, afterSequence), grainFactory);
        var earlyResult = queryResult.Outcome switch
        {
            ConversationQueryOutcome.Found => (IResult?)null,
            ConversationQueryOutcome.NotFound => NotFound(),
            ConversationQueryOutcome.Invalid => BadRequest(queryResult.Reason),
            _ => throw new InvalidOperationException($"Unhandled {nameof(ConversationQueryOutcome)} '{queryResult.Outcome}' for events."),
        };
        if (earlyResult is not null)
            return earlyResult;

        var address = new ConversationAddress(DevTenantId, id);
        await sseWriter.WriteAsync(response, address, afterSequence, ct);
        return Results.Empty;
    }

    // ══════════════════════════════════════ Shared helpers ═════════════════════════════════════

    private static bool TryParseRouteId(string raw, out Guid id) => Guid.TryParse(raw, out id) && id != Guid.Empty;

    /// <summary>Null for an uninitialized/no-mission grain — a snapshot alone can never distinguish
    /// that from a genuinely empty checkpoint, so callers must never leak it as <c>200</c>.</summary>
    private static async Task<ConversationSnapshot?> TryGetExistingSnapshotAsync(IConversationGrain grain)
    {
        var result = await grain.GetSnapshotAsync();
        var snapshot = JsonSerializer.Deserialize(result.SnapshotJson, ConversationContractsJsonContext.Default.ConversationSnapshot)!;
        return string.IsNullOrEmpty(snapshot.MissionRef) ? null : snapshot;
    }

    private static ConversationCommandOutcomeResult Invalid(string reason)
        => new(ConversationCommandOutcome.Invalid, null, reason);

    private static IResult BadRequest(string? reason)
        => Results.Text(reason ?? "Bad request.", "text/plain", statusCode: StatusCodes.Status400BadRequest);

    private static IResult NotFound() => Results.NotFound();

    private static IResult Conflict(string? reason)
        => Results.Text(reason ?? "Conflict.", "text/plain", statusCode: StatusCodes.Status409Conflict);

    /// <summary>A <c>202 Accepted</c> response carrying the documented <c>Location</c> and
    /// <c>Retry-After: 1</c> headers — <see cref="Results.Accepted"/> alone does not set
    /// <c>Retry-After</c>.</summary>
    private static IResult AcceptedWithHeaders<T>(T value, string location) where T : notnull
        => new AcceptedWithHeadersResult<T>(value, location);

    private sealed class AcceptedWithHeadersResult<T>(T value, string location) : IResult where T : notnull
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = StatusCodes.Status202Accepted;
            httpContext.Response.Headers["Location"] = location;
            httpContext.Response.Headers["Retry-After"] = "1";
            return httpContext.Response.WriteAsJsonAsync(value, ConversationContractsJsonContext.Default.Options, httpContext.RequestAborted);
        }
    }
}
