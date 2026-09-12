using System.Text.Json;
using System.Text;
using Azure;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.ConversationHost.Persistence;
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

    /// <summary>The closed Project mission catalog (43.21 task 1). Re-checked here because the
    /// Host is a public entry point and must not depend on a caller having validated its input;
    /// the Worker's own resolver is the third and final check.</summary>
    public static void MapConversationApi(this WebApplication app)
    {
        app.MapPost("/conversations", StartConversationAsync);
        app.MapPost("/conversations/{conversationId}/commands", SubmitCommandAsync);
        app.MapPost("/conversations/{conversationId}/tool-results", SubmitToolResultAsync);
        app.MapGet("/conversations/{conversationId}", GetConversationAsync);
        app.MapGet("/conversations/{conversationId}/events", StreamEventsAsync);
        // 43.21 task 1 — the universal Project Mission invocation pair. Both produce ordinary
        // runs, so their responses are the same shape a Janus start already returns.
        app.MapPost("/conversations/project-mission", (CreateProjectMissionContainerRequest request, IGrainFactory grains) =>
            ProjectRouteAsync(() => CreateProjectMissionContainerAsync(request, grains)));
        app.MapPost("/conversations/{containerId}/mission-runs", (string containerId, StartProjectMissionRunRequest request, IGrainFactory grains) =>
            ProjectRouteAsync(() => StartProjectMissionRunAsync(containerId, request, grains)));
        app.MapPost("/mission-conversations", (CreateMissionConversationRequest request, IGrainFactory grains, IProjectMissionConversationDirectoryStore directory) =>
            ProjectRouteAsync(() => CreateMissionConversationAsync(request, grains, directory)));
        app.MapGet("/mission-conversations/{projectId}", (string projectId, IGrainFactory grains, IProjectMissionConversationDirectoryStore directory) =>
            ProjectRouteAsync(() => ListMissionConversationsAsync(projectId, grains, directory)));
        // Phase 48 — one named bounded history read for an existing Mission Conversation. `through`
        // is required here, unlike the run-trace route's optional form: the caller's fixed upper
        // bound is what makes a multi-page assembly gapless.
        app.MapGet("/mission-conversations/{conversationId}/events", (string conversationId, string? after, string? through, IGrainFactory grains) =>
            ProjectRouteAsync(() => ReadMissionConversationEventsAsync(conversationId, after, through, grains)));
        app.MapPost("/mission-conversations/{conversationId}/turns", (string conversationId, SubmitMissionTurnRequest request, IGrainFactory grains) =>
            ProjectRouteAsync(() => SubmitMissionTurnAsync(conversationId, request, grains)));
        app.MapPost("/mission-conversations/{conversationId}/turns/retry", (string conversationId, RetryMissionTurnRequest request, IGrainFactory grains) =>
            ProjectRouteAsync(() => RetryMissionTurnAsync(conversationId, request, grains)));
        app.MapPost("/mission-conversations/{conversationId}/turns/cancel", (string conversationId, CancelMissionTurnRequest request, IGrainFactory grains) =>
            ProjectRouteAsync(() => CancelMissionTurnAsync(conversationId, request, grains)));
        app.MapPost("/evaluations", (StartEvaluationRequest request, IGrainFactory grains) =>
            ProjectRouteAsync(() => StartEvaluationAsync(request, grains)));
        app.MapGet("/evaluations/{evaluationResultId}", (string evaluationResultId, IGrainFactory grains) =>
            ProjectRouteAsync(() => GetEvaluationAsync(evaluationResultId, grains)));
        app.MapGet("/conversations/{containerId}/runs", (string containerId, string? anchor, string? before, IGrainFactory grains) =>
            ProjectRouteAsync(() => ReadProjectRunsAsync(containerId, anchor, before, grains)));
        app.MapGet("/conversations/{containerId}/runs/{runId}", (string containerId, string runId, IGrainFactory grains) =>
            ProjectRouteAsync(() => ReadProjectRunAsync(containerId, runId, grains)));
        app.MapGet("/conversations/{containerId}/runs/{runId}/events", (string containerId, string runId, string? after, string? through, IGrainFactory grains) =>
            ProjectRouteAsync(() => ReadProjectRunEventsAsync(containerId, runId, after, through, grains)));
        app.MapGet("/conversations/{containerId}/project-commands/{commandId}", (string containerId, string commandId, IGrainFactory grains) =>
            ProjectRouteAsync(() => ReadProjectCommandAsync(containerId, commandId, grains)));
        // Generic mission hands is additive. It deliberately does not alter the legacy
        // /conversations adapter, which is removed only after Task C's generic Worker route lands.
        app.MapPost("/mission-hands/attach", (AttachMissionHandsRequest request, IGrainFactory grains) =>
            MissionHandsRouteAsync(() => HandleAttachMissionHandsAsync(request, grains)));
        app.MapPost("/mission-hands/detach", (DetachMissionHandsRequest request, IGrainFactory grains) =>
            MissionHandsRouteAsync(() => HandleDetachMissionHandsAsync(request, grains)));
        app.MapPost("/mission-hands/result", (SubmitMissionToolResultRequest request, IGrainFactory grains) =>
            MissionHandsRouteAsync(() => HandleSubmitMissionHandsResultAsync(request, grains)));
        app.MapPost("/mission-hands/work", (GetMissionHandsWorkRequest request, IGrainFactory grains) =>
            MissionHandsWorkRouteAsync(() => HandleGetMissionHandsWorkAsync(request, grains)));
        app.MapPost("/mission-hands/claim", (ClaimMissionHandsWorkRequest request, IGrainFactory grains) =>
            MissionHandsWorkRouteAsync(() => HandleClaimMissionHandsWorkAsync(request, grains)));
        app.MapPost("/mission-hands/confirmation", (BeginMissionHandsConfirmationRequest request, IGrainFactory grains) =>
            MissionHandsRouteAsync(() => HandleBeginMissionHandsConfirmationAsync(request, grains)));
        app.MapPost("/mission-hands/cancel", (CancelMissionHandsAttemptRequest request, IGrainFactory grains) =>
            MissionHandsRouteAsync(() => HandleCancelMissionHandsAttemptAsync(request, grains)));
        app.MapPost("/mission-hands/recover", (RecoverMissionHandsInFlightRequest request, IGrainFactory grains) =>
            MissionHandsRouteAsync(() => HandleRecoverMissionHandsInFlightAsync(request, grains)));
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

    /// <summary>Creates a Project's Mission container idempotently. The container and its required
    /// create command are both derived from the stable Project ID, so every retry reaches the same
    /// grain and another caller cannot mint a second container for that Project.</summary>
    public static async Task<ConversationCommandOutcomeResult> HandleCreateProjectMissionContainerAsync(
        CreateProjectMissionContainerRequest request, IGrainFactory grainFactory)
    {
        if (request.ProjectId == Guid.Empty || request.CommandId == Guid.Empty || string.IsNullOrWhiteSpace(request.ProjectGoal))
            return Invalid("projectId, commandId, and a non-empty projectGoal are required.");

        var createCommandId = ConversationDeterministicIds.ProjectMissionContainerCreate(request.ProjectId);
        if (request.CommandId != createCommandId)
            return Invalid("commandId must be the deterministic Project Mission container command for projectId.");

        var containerId = ConversationDeterministicIds.Conversation(createCommandId);
        var address = new ConversationAddress(DevTenantId, containerId);

        var grain = grainFactory.GetGrain<IConversationGrain>(address.PartitionKey);
        return await grain.AcceptProjectMissionContainerCreateAsync(
            new ConversationProjectMissionCreateInput(request.CommandId, request.ProjectId, request.ProjectGoal));
    }

    /// <summary>Starts one child Mission Run (43.21 task 1). The mission is allow-listed HERE as
    /// well as in Client Runtime and again by the Worker's closed catalog — the Host is a public
    /// entry point, so it does not rely on a caller having validated anything. A container that
    /// exists but is a run or control conversation is a conflict, not a not-found: it is
    /// addressable, it is simply not a Project Mission container.
    ///
    /// This route grants no local tool authority. That is enforced by absence rather than by a
    /// check: the request carries no capability field, so a direct Host caller — the one this
    /// method exists to distrust — has nothing to put a tool declaration in.</summary>
    public static async Task<ConversationCommandOutcomeResult> HandleStartProjectMissionRunAsync(
        StartProjectMissionRunRequest request, IGrainFactory grainFactory)
    {
        if (request.ContainerId == Guid.Empty || request.CommandId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Mission) || string.IsNullOrWhiteSpace(request.Input))
            return Invalid("containerId, commandId, mission, and non-blank input are required.");

        string? packageReason = null;
        if (request.Launch is null)
        {
            if (!ProjectMissionNames.IsKnown(request.Mission))
                return Invalid($"Unsupported mission '{request.Mission}'; only {string.Join(" and ", ProjectMissionNames.All)} are accepted.");
        }
        else if (!string.Equals(request.Mission, "Durable", StringComparison.Ordinal) ||
                 !DurableMissionPackageAdmission.TryValidate(request.Launch, out packageReason))
            return Invalid(packageReason ?? "Generic durable runs use the fixed Durable mission reference.");

        if (request.Input.Length > 32_000 || Encoding.UTF8.GetByteCount(request.Input) > (request.Launch is null ? 16_384 : 4_096))
            return Invalid("Project Mission input exceeds its supported size.");

        var address = new ConversationAddress(DevTenantId, request.ContainerId);
        var grain = grainFactory.GetGrain<IConversationGrain>(address.PartitionKey);

        if (await TryGetExistingSnapshotAsync(grain) is not { } snapshot)
            return new ConversationCommandOutcomeResult(ConversationCommandOutcome.NotFound, null, "Container not found.");

        if (snapshot.Purpose != ConversationPurpose.ProjectMission)
            return new ConversationCommandOutcomeResult(
                ConversationCommandOutcome.Conflict, null, "This conversation is not a Project Mission container.");

        // No capabilities are read, because the request has none to read and the grain declares
        // zero for every run on this route.
        return await grain.AcceptProjectMissionRunAsync(
            new ConversationProjectMissionRunInput(request.CommandId, request.Mission, request.Input,
                request.Launch is null ? null : JsonSerializer.Serialize(request.Launch, ConversationContractsJsonContext.Default.DurableMissionLaunch)));
    }

    public static async Task<ConversationCommandOutcomeResult> HandleCreateMissionConversationAsync(
        CreateMissionConversationRequest request, IGrainFactory grains)
    {
        string? reason = null;
        if (request.ProjectId == Guid.Empty || request.CommandId == Guid.Empty || !DurableMissionPackageAdmission.TryValidate(request.Launch, out reason))
            return Invalid(reason ?? "A Mission Conversation requires Project id, command id, and immutable launch.");
        var id = ConversationDeterministicIds.MissionConversation(request.CommandId);
        return await grains.GetGrain<IConversationGrain>(new ConversationAddress(DevTenantId, id).PartitionKey)
            .AcceptMissionConversationCreateAsync(new MissionConversationCreateInput(request.CommandId, request.ProjectId,
                JsonSerializer.Serialize(request.Launch, ConversationContractsJsonContext.Default.DurableMissionLaunch)));
    }

    public static async Task<ConversationCommandOutcomeResult> HandleStartEvaluationAsync(StartEvaluationRequest request, IGrainFactory grains)
    {
        string? reason = null;
        if (request.EvaluationResultId == Guid.Empty || !DurableMissionPackageAdmission.TryValidate(request.Launch, out reason))
            return Invalid(reason ?? "The evaluation request is invalid.");
        var id = ConversationDeterministicIds.EvaluationConversation(request.EvaluationResultId);
        return await grains.GetGrain<IConversationGrain>(new ConversationAddress(DevTenantId, id).PartitionKey)
            .AcceptEvaluationCreateAsync(new EvaluationCreateInput(JsonSerializer.Serialize(request, ConversationContractsJsonContext.Default.StartEvaluationRequest)));
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

    public static Task<MissionHandsResult> HandleAttachMissionHandsAsync(AttachMissionHandsRequest request, IGrainFactory grains) =>
        HandleMissionHandsAsync(request.ConversationId, request, ConversationContractsJsonContext.Default.AttachMissionHandsRequest,
            static (grain, input) => grain.AttachMissionHandsAsync(input), grains);

    public static Task<MissionHandsResult> HandleDetachMissionHandsAsync(DetachMissionHandsRequest request, IGrainFactory grains) =>
        HandleMissionHandsAsync(request.ConversationId, request, ConversationContractsJsonContext.Default.DetachMissionHandsRequest,
            static (grain, input) => grain.DetachMissionHandsAsync(input), grains);

    public static Task<MissionHandsResult> HandleSubmitMissionHandsResultAsync(SubmitMissionToolResultRequest request, IGrainFactory grains) =>
        HandleMissionHandsAsync(request.ConversationId, request, ConversationContractsJsonContext.Default.SubmitMissionToolResultRequest,
            static (grain, input) => grain.AcceptMissionHandsResultAsync(input), grains);

    public static async Task<MissionHandsWorkItem> HandleGetMissionHandsWorkAsync(GetMissionHandsWorkRequest request, IGrainFactory grains)
    {
        if (request.ConversationId == Guid.Empty)
            return new MissionHandsWorkItem(MissionHandsStatus.AwaitingHands, null, null, null, "conversationId is required.");
        var grain = grains.GetGrain<IConversationGrain>(new ConversationAddress(DevTenantId, request.ConversationId).PartitionKey);
        var result = await grain.GetMissionHandsWorkAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            request, ConversationContractsJsonContext.Default.GetMissionHandsWorkRequest)));
        return JsonSerializer.Deserialize(result.ResultJson, ConversationContractsJsonContext.Default.MissionHandsWorkItem)
            ?? throw new InvalidOperationException("Mission hands grain returned an invalid work item.");
    }

    public static async Task<MissionHandsWorkItem> HandleClaimMissionHandsWorkAsync(ClaimMissionHandsWorkRequest request, IGrainFactory grains)
    {
        if (request.ConversationId == Guid.Empty)
            return new MissionHandsWorkItem(MissionHandsStatus.AwaitingHands, null, null, null, "conversationId is required.");
        var grain = grains.GetGrain<IConversationGrain>(new ConversationAddress(DevTenantId, request.ConversationId).PartitionKey);
        var result = await grain.ClaimMissionHandsWorkAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            request, ConversationContractsJsonContext.Default.ClaimMissionHandsWorkRequest)));
        return JsonSerializer.Deserialize(result.ResultJson, ConversationContractsJsonContext.Default.MissionHandsWorkItem)
            ?? throw new InvalidOperationException("Mission hands grain returned an invalid work item.");
    }

    public static Task<MissionHandsResult> HandleBeginMissionHandsConfirmationAsync(BeginMissionHandsConfirmationRequest request, IGrainFactory grains) =>
        HandleMissionHandsAsync(request.ConversationId, request, ConversationContractsJsonContext.Default.BeginMissionHandsConfirmationRequest,
            static (grain, input) => grain.BeginMissionHandsConfirmationAsync(input), grains);

    public static Task<MissionHandsResult> HandleCancelMissionHandsAttemptAsync(CancelMissionHandsAttemptRequest request, IGrainFactory grains) =>
        HandleMissionHandsAsync(request.ConversationId, request, ConversationContractsJsonContext.Default.CancelMissionHandsAttemptRequest,
            static (grain, input) => grain.CancelMissionHandsAttemptAsync(input), grains);

    public static Task<MissionHandsResult> HandleRecoverMissionHandsInFlightAsync(RecoverMissionHandsInFlightRequest request, IGrainFactory grains) =>
        HandleMissionHandsAsync(request.ConversationId, request, ConversationContractsJsonContext.Default.RecoverMissionHandsInFlightRequest,
            static (grain, input) => grain.RecoverMissionHandsInFlightAsync(input), grains);

    private static async Task<MissionHandsResult> HandleMissionHandsAsync<T>(Guid conversationId, T request,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type,
        Func<IConversationGrain, MissionHandsJsonInput, Task<MissionHandsGrainResult>> operation, IGrainFactory grains)
    {
        if (conversationId == Guid.Empty)
            return new MissionHandsResult(MissionHandsStatus.AwaitingHands, null, "conversationId is required.");
        var grain = grains.GetGrain<IConversationGrain>(new ConversationAddress(DevTenantId, conversationId).PartitionKey);
        var result = await operation(grain, new MissionHandsJsonInput(JsonSerializer.Serialize(request, type)));
        return JsonSerializer.Deserialize(result.ResultJson, ConversationContractsJsonContext.Default.MissionHandsResult)
            ?? throw new InvalidOperationException("Mission hands grain returned an invalid result.");
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
                    result.Acceptance.ConversationId, result.Acceptance.RunId!.Value, result.Acceptance.AcceptedSequence, result.Acceptance.Status)),
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
                    result.Acceptance!.ConversationId, result.Acceptance.RunId!.Value, result.Acceptance.AcceptedSequence, result.Acceptance.Status),
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
                    result.Acceptance!.ConversationId, result.Acceptance.RunId!.Value, result.Acceptance.AcceptedSequence, result.Acceptance.Status),
                $"/conversations/{result.Acceptance.ConversationId}"),
            ConversationCommandOutcome.Invalid => BadRequest(result.Reason),
            ConversationCommandOutcome.NotFound => NotFound(),
            ConversationCommandOutcome.Conflict => Conflict(result.Reason),
            _ => throw new InvalidOperationException($"Unhandled {nameof(ConversationCommandOutcome)} '{result.Outcome}' for tool-result."),
        };
    }

    private static async Task<IResult> CreateProjectMissionContainerAsync(
        CreateProjectMissionContainerRequest request, IGrainFactory grainFactory)
    {
        var result = await HandleCreateProjectMissionContainerAsync(request, grainFactory);
        return result.Outcome switch
        {
            ConversationCommandOutcome.Accepted => Results.Created(
                $"/conversations/{result.Acceptance!.ConversationId}",
                new CreateProjectMissionContainerResponse(
                    result.Acceptance.ConversationId, result.Acceptance.AcceptedSequence)),
            ConversationCommandOutcome.Invalid => ProjectError("invalidRequest", result.Reason ?? "Invalid Project Mission create.", StatusCodes.Status400BadRequest),
            ConversationCommandOutcome.Conflict => ProjectError("commandConflict", result.Reason ?? "Project Mission create conflicts.", StatusCodes.Status409Conflict),
            _ => throw new InvalidOperationException(
                $"Unhandled {nameof(ConversationCommandOutcome)} '{result.Outcome}' for project-mission create."),
        };
    }

    private static async Task<IResult> StartProjectMissionRunAsync(
        string containerId, StartProjectMissionRunRequest request, IGrainFactory grainFactory)
    {
        if (!TryParseRouteId(containerId, out var routeId))
            return ProjectError("invalidRequest", "containerId must be a valid, non-empty GUID.", StatusCodes.Status400BadRequest);
        if (request.ContainerId != routeId)
            return ProjectError("invalidRequest", "Route containerId and request body ContainerId must agree.", StatusCodes.Status400BadRequest);
        string? packageReason = null;
        if (request.Launch is null && !ProjectMissionNames.IsKnown(request.Mission))
            return ProjectError("unknownMission", "The selected mission is not supported.", StatusCodes.Status400BadRequest);
        if (request.Launch is not null && (!string.Equals(request.Mission, "Durable", StringComparison.Ordinal) ||
            !DurableMissionPackageAdmission.TryValidate(request.Launch, out packageReason)))
            return ProjectError("invalidRequest", packageReason ?? "The immutable durable package is invalid.", StatusCodes.Status400BadRequest);
        if (string.IsNullOrWhiteSpace(request.Input) || request.Input.Length > 32_000 || Encoding.UTF8.GetByteCount(request.Input) > 16_384)
            return ProjectError("invalidRequest", "Project Mission input is invalid.", StatusCodes.Status400BadRequest);

        var result = await HandleStartProjectMissionRunAsync(request, grainFactory);
        return result.Outcome switch
        {
            ConversationCommandOutcome.Accepted => AcceptedWithHeaders(
                new StartProjectMissionRunResponse(
                    result.Acceptance!.ConversationId, result.Acceptance.RunId!.Value,
                    result.Acceptance.AcceptedSequence, result.Acceptance.Status),
                $"/conversations/{result.Acceptance.ConversationId}"),
            ConversationCommandOutcome.Invalid => ProjectError("invalidRequest", result.Reason ?? "Invalid Project Mission run.", StatusCodes.Status400BadRequest),
            ConversationCommandOutcome.NotFound => ProjectError("notFound", "The Project Mission container was not found.", StatusCodes.Status404NotFound),
            // Both map to 409, but they are kept as separate arms rather than merged: a surface
            // needs to tell "one run at a time" apart from a genuinely contradictory request, and
            // the reason text is what carries that.
            ConversationCommandOutcome.RunAlreadyActive => ProjectError("runAlreadyActive", result.Reason ?? "A run is active.", StatusCodes.Status409Conflict),
            ConversationCommandOutcome.Conflict => ProjectError("commandConflict", result.Reason ?? "Project Mission run conflicts.", StatusCodes.Status409Conflict),
            _ => throw new InvalidOperationException(
                $"Unhandled {nameof(ConversationCommandOutcome)} '{result.Outcome}' for project-mission run."),
        };
    }

    private static async Task<IResult> CreateMissionConversationAsync(CreateMissionConversationRequest request,
        IGrainFactory grains, IProjectMissionConversationDirectoryStore directory)
    {
        var result = await HandleCreateMissionConversationAsync(request, grains);
        if (result.Outcome != ConversationCommandOutcome.Accepted)
            return result.Outcome == ConversationCommandOutcome.Invalid ? ProjectError("invalidRequest", result.Reason ?? "Invalid Mission Conversation.", 400) :
                ProjectError("commandConflict", result.Reason ?? "Mission Conversation conflicts.", 409);
        var snapshot = await SnapshotAsync(result.Acceptance!.ConversationId, grains);
        var summary = new MissionConversationSummary(snapshot.ConversationId, request.ProjectId, snapshot.PinnedLaunch!, snapshot.Status,
            snapshot.LastSequence, snapshot.UpdatedAtUtc, snapshot.Title);
        await directory.UpsertAsync(DevTenantId, summary, CancellationToken.None);
        return Results.Created($"/conversations/{summary.ConversationId}", new CreateMissionConversationResponse(summary.ConversationId, summary.LastSequence, summary.Launch));
    }

    private static async Task<IResult> ListMissionConversationsAsync(string projectId, IGrainFactory grains,
        IProjectMissionConversationDirectoryStore directory)
    {
        if (!TryParseRouteId(projectId, out var id)) return ProjectError("invalidRequest", "projectId is required.", 400);
        var stored = await directory.ReadAsync(DevTenantId, id, CancellationToken.None);
        var valid = new List<MissionConversationSummary>();
        foreach (var item in stored)
        {
            var snapshot = await TryGetExistingSnapshotAsync(grains.GetGrain<IConversationGrain>(new ConversationAddress(DevTenantId, item.ConversationId).PartitionKey));
            if (snapshot?.Purpose != ConversationPurpose.MissionConversation || snapshot.ProjectId != id || snapshot.PinnedLaunch is null)
            {
                await directory.RemoveAsync(DevTenantId, id, item.ConversationId, CancellationToken.None);
                continue;
            }
            valid.Add(new MissionConversationSummary(snapshot.ConversationId, id, snapshot.PinnedLaunch, snapshot.Status,
                snapshot.LastSequence, snapshot.UpdatedAtUtc, snapshot.Title));
        }
        return Results.Ok(new ListMissionConversationsResponse([.. valid]));
    }

    private static async Task<IResult> ReadMissionConversationEventsAsync(string conversationId, string? after, string? through,
        IGrainFactory grains)
    {
        if (!TryParseRouteId(conversationId, out var id) || !TryParseRange(after, through, out var parsedAfter, out var parsedThrough) ||
            parsedThrough is null)
            return ProjectError("invalidRequest", "after and through are required and must bound a valid range.", 400);
        var grain = grains.GetGrain<IConversationGrain>(new ConversationAddress(DevTenantId, id).PartitionKey);
        if (await TryGetExistingSnapshotAsync(grain) is null)
            return ProjectError("notFound", "Conversation not found.", 404);
        var result = await grain.ReadMissionConversationEventsAsync(parsedAfter, parsedThrough.Value);
        return ProjectReadResult(result, ConversationContractsJsonContext.Default.MissionConversationEventPage);
    }

    private static async Task<IResult> SubmitMissionTurnAsync(string conversationId, SubmitMissionTurnRequest request, IGrainFactory grains)
    {
        if (!TryParseRouteId(conversationId, out var id) || request.ConversationId != id)
            return ProjectError("invalidRequest", "conversationId is invalid.", 400);
        var grain = grains.GetGrain<IConversationGrain>(new ConversationAddress(DevTenantId, id).PartitionKey);
        var result = await grain.AcceptMissionConversationTurnAsync(new MissionConversationTurnInput(request.CommandId, null, false, request.Text));
        return result.Outcome switch
        {
            ConversationCommandOutcome.Accepted => Results.Accepted($"/conversations/{id}", new SubmitMissionTurnResponse(id, result.Acceptance!.TurnId!.Value,
                result.Acceptance.RunId!.Value, result.Acceptance.AcceptedSequence, result.Acceptance.Status)),
            ConversationCommandOutcome.RunAlreadyActive => ProjectError("runAlreadyActive", result.Reason ?? "A turn is active.", 409),
            ConversationCommandOutcome.NotFound => ProjectError("notFound", result.Reason ?? "Conversation not found.", 404),
            _ => ProjectError("commandConflict", result.Reason ?? "Turn conflicts.", 409),
        };
    }

    private static async Task<IResult> RetryMissionTurnAsync(string conversationId, RetryMissionTurnRequest request, IGrainFactory grains)
    {
        if (!TryParseRouteId(conversationId, out var id) || request.ConversationId != id) return ProjectError("invalidRequest", "conversationId is invalid.", 400);
        var result = await grains.GetGrain<IConversationGrain>(new ConversationAddress(DevTenantId, id).PartitionKey)
            .AcceptMissionConversationTurnAsync(new MissionConversationTurnInput(request.CommandId, request.TurnId, true, null));
        return result.Outcome == ConversationCommandOutcome.Accepted ? Results.Accepted($"/conversations/{id}", new SubmitMissionTurnResponse(id, result.Acceptance!.TurnId!.Value, result.Acceptance.RunId!.Value, result.Acceptance.AcceptedSequence, result.Acceptance.Status)) : ProjectError("commandConflict", result.Reason ?? "Retry conflicts.", 409);
    }

    private static async Task<IResult> CancelMissionTurnAsync(string conversationId, CancelMissionTurnRequest request, IGrainFactory grains)
    {
        if (!TryParseRouteId(conversationId, out var id) || request.ConversationId != id) return ProjectError("invalidRequest", "conversationId is invalid.", 400);
        var result = await grains.GetGrain<IConversationGrain>(new ConversationAddress(DevTenantId, id).PartitionKey)
            .CancelMissionConversationTurnAsync(new MissionConversationCancelInput(request.CommandId, request.TurnId, request.TurnAttemptId));
        return result.Outcome == ConversationCommandOutcome.Accepted
            ? Results.Accepted($"/conversations/{id}", new CancelMissionTurnResponse(id, request.TurnId,
                request.TurnAttemptId, result.Acceptance!.AcceptedSequence, result.Acceptance.Status))
            : ProjectError("commandConflict", result.Reason ?? "Cancel conflicts.", 409);
    }

    private static async Task<IResult> StartEvaluationAsync(StartEvaluationRequest request, IGrainFactory grains)
    {
        var result = await HandleStartEvaluationAsync(request, grains);
        if (result.Outcome != ConversationCommandOutcome.Accepted)
            return ProjectError("invalidRequest", result.Reason ?? "Evaluation rejected.", 400);
        return Results.Accepted($"/evaluations/{request.EvaluationResultId}", new StartEvaluationResponse(
            await EvaluationProjectionAsync(request.EvaluationResultId, grains)));
    }

    private static async Task<IResult> GetEvaluationAsync(string evaluationResultId, IGrainFactory grains)
    {
        if (!TryParseRouteId(evaluationResultId, out var id)) return ProjectError("invalidRequest", "evaluationResultId is required.", 400);
        var projection = await EvaluationProjectionAsync(id, grains);
        return projection.ConversationId == Guid.Empty ? ProjectError("notFound", "Evaluation not found.", 404) : Results.Ok(new GetEvaluationResponse(projection));
    }

    private static async Task<ConversationSnapshot> SnapshotAsync(Guid conversationId, IGrainFactory grains) =>
        (await HandleGetConversationAsync(new GetConversationRequest(conversationId), grains)).Response!.Snapshot;

    private static async Task<EvaluationProjection> EvaluationProjectionAsync(Guid resultId, IGrainFactory grains)
    {
        var conversationId = ConversationDeterministicIds.EvaluationConversation(resultId);
        var snapshot = await TryGetExistingSnapshotAsync(grains.GetGrain<IConversationGrain>(new ConversationAddress(DevTenantId, conversationId).PartitionKey));
        if (snapshot?.Purpose != ConversationPurpose.Evaluation || snapshot.EvaluationResultId != resultId || snapshot.EvaluationTurnId is not { } turn || snapshot.EvaluationTurnAttemptId is not { } attempt)
            return new EvaluationProjection(resultId, Guid.Empty, Guid.Empty, Guid.Empty, false, null, null, null, null);
        var terminal = snapshot.Status is ConversationRunStatus.Completed or ConversationRunStatus.Failed or ConversationRunStatus.Interrupted or ConversationRunStatus.Rejected;
        EvaluationOutcomeWire? outcome = !terminal ? null : snapshot.Status == ConversationRunStatus.Completed ? EvaluationOutcomeWire.Succeeded : EvaluationOutcomeWire.Failed;
        var trace = terminal ? new EvaluationTraceOriginWire(conversationId, turn, attempt) : null;
        var reason = snapshot.EvaluationReason ?? (terminal && outcome == EvaluationOutcomeWire.Failed
            ? $"Evaluation ended with {snapshot.Status}." : null);
        var summary = snapshot.EvaluationSummary ?? reason ?? (terminal ? "Evaluation completed." : null);
        return new EvaluationProjection(resultId, conversationId, turn, attempt, terminal, outcome,
            summary, trace, reason);
    }

    private static async Task<IResult> ReadProjectRunsAsync(string containerId, string? anchor, string? before, IGrainFactory grainFactory)
    {
        if (!TryParseRouteId(containerId, out var id) || !TryParseCursor(anchor, before, out var parsedAnchor, out var parsedBefore))
            return ProjectError("invalidRequest", "The runs cursor is invalid.", StatusCodes.Status400BadRequest);
        var grain = ProjectGrain(id, grainFactory);
        if (await ProjectContainerErrorAsync(grain) is { } error) return error;
        var result = await grain.ReadProjectRunsAsync(parsedAnchor, parsedBefore);
        return ProjectReadResult(result, ConversationContractsJsonContext.Default.ProjectRunPage);
    }

    private static async Task<IResult> ReadProjectRunAsync(string containerId, string runId, IGrainFactory grainFactory)
    {
        if (!TryParseRouteId(containerId, out var container) || !TryParseRouteId(runId, out var run))
            return ProjectError("invalidRequest", "Container and run ids are required.", StatusCodes.Status400BadRequest);
        var grain = ProjectGrain(container, grainFactory);
        if (await ProjectContainerErrorAsync(grain) is { } error) return error;
        var result = await grain.ReadProjectRunAsync(run);
        return ProjectReadResult(result, ConversationContractsJsonContext.Default.ProjectRunDetail);
    }

    private static async Task<IResult> ReadProjectRunEventsAsync(string containerId, string runId, string? after, string? through, IGrainFactory grainFactory)
    {
        if (!TryParseRouteId(containerId, out var container) || !TryParseRouteId(runId, out var run) ||
            !TryParseRange(after, through, out var parsedAfter, out var parsedThrough))
            return ProjectError("invalidRequest", "The trace range is invalid.", StatusCodes.Status400BadRequest);
        var grain = ProjectGrain(container, grainFactory);
        if (await ProjectContainerErrorAsync(grain) is { } error) return error;
        var result = await grain.ReadProjectRunEventsAsync(run, parsedAfter, parsedThrough);
        return ProjectReadResult(result, ConversationContractsJsonContext.Default.ProjectRunEventPage);
    }

    private static async Task<IResult> ReadProjectCommandAsync(string containerId, string commandId, IGrainFactory grainFactory)
    {
        if (!TryParseRouteId(containerId, out var container) || !TryParseRouteId(commandId, out var command))
            return ProjectError("invalidRequest", "Container and command ids are required.", StatusCodes.Status400BadRequest);
        var grain = ProjectGrain(container, grainFactory);
        if (await ProjectContainerErrorAsync(grain) is { } error) return error;
        var result = await grain.ReadProjectCommandAsync(command);
        return ProjectReadResult(result, ConversationContractsJsonContext.Default.ProjectCommandReceipt);
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

    private static IConversationGrain ProjectGrain(Guid id, IGrainFactory factory) =>
        factory.GetGrain<IConversationGrain>(new ConversationAddress(DevTenantId, id).PartitionKey);

    private static async Task<IResult?> ProjectContainerErrorAsync(IConversationGrain grain)
    {
        var snapshot = await TryGetExistingSnapshotAsync(grain);
        if (snapshot is null) return ProjectError("notFound", "The Project Mission container was not found.", StatusCodes.Status404NotFound);
        return snapshot.Purpose == ConversationPurpose.ProjectMission
            ? null
            : ProjectError("wrongPurpose", "This conversation is not a Project Mission container.", StatusCodes.Status409Conflict);
    }

    private static bool TryParseCursor(string? anchor, string? before, out long? parsedAnchor, out long? parsedBefore)
    {
        parsedAnchor = null; parsedBefore = null;
        if (string.IsNullOrEmpty(anchor) && string.IsNullOrEmpty(before)) return true;
        if (string.IsNullOrEmpty(anchor) || string.IsNullOrEmpty(before) ||
            !long.TryParse(anchor, out var a) || !long.TryParse(before, out var b) || a <= 0 || b <= 0)
            return false;
        parsedAnchor = a; parsedBefore = b; return true;
    }

    private static bool TryParseRange(string? after, string? through, out long parsedAfter, out long? parsedThrough)
    {
        parsedAfter = 0; parsedThrough = null;
        if (!string.IsNullOrEmpty(after) && (!long.TryParse(after, out parsedAfter) || parsedAfter < 0)) return false;
        if (!string.IsNullOrEmpty(through) && (!long.TryParse(through, out var t) || t < 0)) return false;
        if (!string.IsNullOrEmpty(through)) parsedThrough = long.Parse(through!);
        return true;
    }

    private static IResult ProjectReadResult<T>(ConversationProjectReadResult result, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type) =>
        result.ErrorCode is { } code ? ProjectError(code, result.ErrorMessage ?? "Project history request failed.", ProjectErrorStatus(code)) :
        JsonSerializer.Deserialize(result.PayloadJson ?? "", type) is { } payload ? Results.Ok(payload) :
        ProjectError("serviceUnavailable", "Project history response was invalid.", StatusCodes.Status503ServiceUnavailable);

    private static IResult ProjectError(string code, string message, int status) =>
        Results.Json(new ConversationApiError(code, message), ConversationContractsJsonContext.Default.ConversationApiError, statusCode: status);

    private static async Task<IResult> ProjectRouteAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (RequestFailedException)
        {
            return ProjectError("serviceUnavailable", "Project Mission storage is temporarily unavailable.", StatusCodes.Status503ServiceUnavailable);
        }
        catch (TimeoutException)
        {
            return ProjectError("serviceUnavailable", "Project Mission service is temporarily unavailable.", StatusCodes.Status503ServiceUnavailable);
        }
        catch (HttpRequestException)
        {
            return ProjectError("serviceUnavailable", "Project Mission service is temporarily unavailable.", StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> MissionHandsRouteAsync(Func<Task<MissionHandsResult>> action)
    {
        try
        {
            var result = await action();
            return result.AcceptedSequence is null && result.Reason is not null
                ? ProjectError("missionHandsConflict", result.Reason, StatusCodes.Status409Conflict)
                : Results.Ok(result);
        }
        catch (TimeoutException)
        {
            return ProjectError("serviceUnavailable", "Mission hands service is temporarily unavailable.", StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> MissionHandsWorkRouteAsync(Func<Task<MissionHandsWorkItem>> action)
    {
        try
        {
            var result = await action();
            // A missing/stale work lease is an ordinary poll result, not a transport failure.
            // Returning its typed reason lets Application reject it before Bob admission.
            return Results.Ok(result);
        }
        catch (TimeoutException)
        {
            return ProjectError("serviceUnavailable", "Mission hands service is temporarily unavailable.", StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static int ProjectErrorStatus(string code) => code switch
    {
        "invalidRequest" or "unknownMission" => StatusCodes.Status400BadRequest,
        "notFound" => StatusCodes.Status404NotFound,
        "legacyReadOnly" => StatusCodes.Status410Gone,
        "serviceUnavailable" => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status409Conflict,
    };

    /// <summary>Null for an uninitialized grain — a snapshot alone can never distinguish that from
    /// a genuinely empty checkpoint, so callers must never leak it as <c>200</c>.
    ///
    /// A pinned mission is what proves a run or control conversation exists. A Project Mission
    /// container pins none by design (43.21 task 1), so its existence invariant is instead its
    /// purpose paired with a non-null Project ID — checking only the mission would report every
    /// created container as missing.</summary>
    private static async Task<ConversationSnapshot?> TryGetExistingSnapshotAsync(IConversationGrain grain)
    {
        var result = await grain.GetSnapshotAsync();
        var snapshot = JsonSerializer.Deserialize(result.SnapshotJson, ConversationContractsJsonContext.Default.ConversationSnapshot)!;

        var exists = !string.IsNullOrEmpty(snapshot.MissionRef) ||
            (snapshot.Purpose is ConversationPurpose.ProjectMission or ConversationPurpose.MissionConversation or ConversationPurpose.Evaluation && snapshot.ProjectId is not null);

        return exists ? snapshot : null;
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
