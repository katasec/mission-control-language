using System.Text.Json;
using ForgeMission.Application;
using ForgeMission.Application.Transport;

namespace ForgeMission.Application.Host;

internal static class ApplicationEndpoints
{
    public static void MapApplicationTransport(this WebApplication app)
    {
        app.MapPost("/transport/session/setup", async (SessionSetupRequest request, IApplicationSessionService service, CancellationToken ct) =>
        {
            try { return Results.Ok(await service.ReplaceAsync(request, ct)); }
            catch (SessionReplacementRejectedException exception) { return Results.BadRequest(exception.Message); }
        });
        app.MapPost("/transport/project/draft", async (ProjectDraftRequest request, IProjectService service, CancellationToken ct) => Results.Ok(await service.DraftAsync(request, ct)));
        app.MapPost("/transport/project/create", async (ProjectCreateRequest request, IProjectService service, CancellationToken ct) => Results.Ok(await service.CreateAsync(request, ct)));
        app.MapPost("/transport/project/open", async (ProjectOpenRequest request, IProjectService service, CancellationToken ct) => Results.Ok(await service.OpenAsync(request, ct)));
        app.MapPost("/transport/project/mission/run", (StartProjectMissionRunRequest request, IMissionSubmissionService service, CancellationToken ct) => Result(service.StartAsync(request, ct)));
        app.MapPost("/transport/project/mission/retry", (RetryProjectMissionSubmissionRequest request, IMissionSubmissionService service, CancellationToken ct) => Result(service.RetryAsync(request, ct)));
        app.MapPost("/transport/project/mission/select", (SelectProjectMissionRequest request, IProjectService service, CancellationToken ct) => Result(service.SelectMissionAsync(request, ct)));
        app.MapPost("/transport/project/workbench", (GetProjectWorkbenchRequest request, IProjectContentService service, CancellationToken ct) => Result(service.GetWorkbenchAsync(request, ct)));
        app.MapPost("/transport/project/document", (OpenProjectDocumentRequest request, IProjectContentService service, CancellationToken ct) => Result(service.OpenDocumentAsync(request, ct)));
        app.MapPost("/transport/project/mission/state", (GetProjectMissionStateRequest request, IRunHistoryService service, CancellationToken ct) => Result(service.GetStateAsync(request, ct)));
        app.MapPost("/transport/project/runs", (GetProjectRunsRequest request, IRunHistoryService service, CancellationToken ct) => Result(service.GetRunsAsync(request, ct)));
        app.MapPost("/transport/project/run", (GetProjectRunRequest request, IRunHistoryService service, CancellationToken ct) => Result(service.GetRunAsync(request, ct)));
        app.MapPost("/transport/project/run/events", (GetProjectRunEventsRequest request, IRunHistoryService service, CancellationToken ct) => Result(service.GetEventsAsync(request, ct)));
        app.MapPost("/transport/capability/dispatch", async (CapabilityDispatchRequest request, ICapabilityActionService service, CancellationToken ct) =>
        {
            try { return Results.Ok(await service.DispatchAsync(request, ct)); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
            catch (ArgumentException exception) { return Results.BadRequest(exception.Message); }
        });
        app.MapPost("/transport/confirmation/respond", (ConfirmationResponseRequest request, IInteractionService service) => Results.Ok(service.Respond(request)));
        app.MapPost("/transport/prompt", (PromptRequest request, IConversationService service, CancellationToken ct) => Result(service.PromptAsync(request, ct)));
        app.MapGet("/transport/events", async (HttpContext context, ApplicationEventHub events, CancellationToken ct) =>
        {
            context.Response.Headers.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            await context.Response.StartAsync(ct);
            await foreach (var message in events.Subscribe(ct))
            {
                var json = JsonSerializer.Serialize(message, ConversationRelayJsonContext.Default.ApplicationEvent);
                await context.Response.WriteAsync($"data: {json}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);
            }
        });
    }

    private static async Task<IResult> Result<T>(Task<T> action)
    {
        try { return Results.Ok(await action); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    }
}
