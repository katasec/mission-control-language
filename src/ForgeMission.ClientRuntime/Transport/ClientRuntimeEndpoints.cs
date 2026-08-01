using System.Text.Json;
using ForgeMission.ClientRuntime.Services;
using ForgeMission.ClientRuntime.Transport;
using ForgeMission.Core.Runtime;
using ForgeMission.Core.Tools;

namespace ForgeMission.ClientRuntime.TransportHost;

internal static class ClientRuntimeEndpoints
{
    public static void MapClientRuntimeTransport(this WebApplication app)
    {
        app.MapPost("/transport/session/setup", (SessionSetupRequest request, ClientRuntimeSessionStore sessions) =>
        {
            var session = sessions.Create(request.WorkspaceRoot);
            return Results.Ok(new SessionSetupResponse(session.Id,
                session.Workspace.Capabilities?.AvailableCapabilities ?? []));
        });

        app.MapPost("/transport/capability/dispatch", async (
            CapabilityDispatchRequest request,
            ClientRuntimeSessionStore sessions,
            ClientRuntimeEventHub events,
            CancellationToken ct) =>
        {
            if (!sessions.TryGet(request.SessionId, out var session) || session?.Workspace.Dispatcher is null)
                return Results.NotFound();

            var capabilityRequest = ToCapabilityRequest(request.Request);
            if (capabilityRequest is null)
                return Results.BadRequest("Unsupported capability request.");

            events.Publish(new ClientRuntimeEvent(ClientRuntimeEventKind.ToolCallStatus, request.SessionId,
                ToolName: request.Request.Operation.ToString(), ToolStatus: "running"));
            var result = await session.Workspace.Dispatcher.DispatchAsync(
                request.Request.CapabilityName, capabilityRequest, ct);
            events.Publish(new ClientRuntimeEvent(ClientRuntimeEventKind.ToolCallStatus, request.SessionId,
                ToolName: request.Request.Operation.ToString(), ToolStatus: result.IsError ? "error" : "done"));
            return Results.Ok(new CapabilityDispatchResponse(result.Content, result.IsError));
        });

        app.MapPost("/transport/confirmation/respond", (
            ConfirmationResponseRequest request,
            ClientRuntimeSessionStore sessions) =>
        {
            var accepted = sessions.TryGet(request.SessionId, out var session) && session is not null &&
                session.Confirmation.Resolve(request.ConfirmationId, request.Approved);
            return Results.Ok(new ConfirmationResponse(accepted));
        });

        app.MapPost("/transport/prompt", async (
            PromptRequest request,
            ClientRuntimeSessionStore sessions,
            IHttpClientFactory clients,
            ClientRuntimeEventHub events,
            CancellationToken ct) =>
        {
            if (!sessions.TryGet(request.SessionId, out var session) ||
                session?.Workspace.Capabilities is null || session.Workspace.Dispatcher is null)
                return Results.NotFound();

            var mission = new MissionRuntimeSession(clients.CreateClient("mission-runtime"));
            try
            {
                var answer = await mission.SendAsync(request.Prompt, session.Workspace.Capabilities,
                    session.Workspace.Dispatcher,
                    text => events.Publish(new ClientRuntimeEvent(ClientRuntimeEventKind.MissionTextDelta,
                        request.SessionId, Text: text)),
                    update => events.Publish(new ClientRuntimeEvent(ClientRuntimeEventKind.ToolCallStatus,
                        request.SessionId, ToolName: update.Call.Name, ToolStatus: update.State.ToString())), ct);
                return Results.Ok(new PromptResponse(answer));
            }
            catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
            {
                events.Publish(new ClientRuntimeEvent(ClientRuntimeEventKind.Error, request.SessionId,
                    Error: exception.Message));
                return Results.Ok(new PromptResponse(exception.Message, IsError: true));
            }
        });

        app.MapGet("/transport/events", async (HttpContext context, ClientRuntimeEventHub events, CancellationToken ct) =>
        {
            context.Response.Headers.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            await context.Response.StartAsync(ct);
            await foreach (var message in events.Subscribe(ct))
            {
                var json = JsonSerializer.Serialize(message, ClientRuntimeJsonContext.Default.ClientRuntimeEvent);
                await context.Response.WriteAsync($"data: {json}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);
            }
        });
    }

    private static ICapabilityRequest? ToCapabilityRequest(CapabilityRequestData request) => request.Operation switch
    {
        CapabilityOperation.ReadFile when request.FilePath is not null =>
            new ReadFileCapabilityRequest(request.FilePath, request.Offset, request.Limit),
        CapabilityOperation.EditFile when request.FilePath is not null && request.OldString is not null && request.NewString is not null =>
            new EditFileCapabilityRequest(request.FilePath, request.OldString, request.NewString, request.ReplaceAll),
        CapabilityOperation.WriteFile when request.FilePath is not null && request.Content is not null =>
            new WriteFileCapabilityRequest(request.FilePath, request.Content),
        CapabilityOperation.ExecuteTerminal when request.Command is not null =>
            new ExecuteTerminalCapabilityRequest(request.Command),
        _ => null,
    };
}
