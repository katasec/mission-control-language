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
        app.MapPost("/transport/session/setup", async (SessionSetupRequest request, ClientRuntimeSessionStore sessions) =>
        {
            var session = await sessions.CreateAsync(
                request.WorkspaceRoot, request.Mission, request.Runtime, request.ReplacesSessionId);
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

            var target = request.Request.FilePath ?? request.Request.Command;
            events.Publish(new ClientRuntimeEvent(ClientRuntimeEventKind.ToolCallStatus, request.SessionId,
                ToolName: request.Request.Operation.ToString(), ToolStatus: "running", ToolTarget: target));
            var result = await session.Workspace.Dispatcher.DispatchAsync(
                request.Request.CapabilityName, capabilityRequest, ct);
            events.Publish(new ClientRuntimeEvent(ClientRuntimeEventKind.ToolCallStatus, request.SessionId,
                ToolName: request.Request.Operation.ToString(), ToolStatus: result.IsError ? "error" : "done", ToolTarget: target));
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
            IConfiguration configuration,
            ClientRuntimeEventHub events,
            IHostApplicationLifetime lifetime,
            CancellationToken ct) =>
        {
            if (!sessions.TryGet(request.SessionId, out var session) ||
                session?.Workspace.Capabilities is null || session.Workspace.Dispatcher is null)
                return Results.NotFound();

            try
            {
                if (session.Runtime == SessionRuntimeKind.DurableConversation)
                {
                    // Admission, lazy creation, and SendAsync must stay one call — see
                    // ConversationSessionSlot for why a separate GetOrCreate-then-SendAsync would
                    // race a concurrent mission-switch replacement.
                    var conversationId = await session.Conversation.SendPromptAsync(
                        () => new ConversationRuntimeSession(
                            request.SessionId,
                            session.Mission ?? "Janus",
                            new ConversationHostClient(clients.CreateClient("conversation-host")),
                            session.Workspace.Capabilities,
                            session.Workspace.Dispatcher,
                            events.Publish,
                            lifetime.ApplicationStopping),
                        request.Prompt, ct);
                    return Results.Ok(new PromptResponse(string.Empty, ConversationId: conversationId));
                }

                var runtimeClient = clients.CreateClient("mission-runtime");
                var answer = UsesCloudMissionRuntime(configuration["MissionRuntime:Mode"])
                    ? await NewCloudSession(runtimeClient, session.Mission).SendAsync(request.Prompt,
                        session.Workspace.Capabilities, session.Workspace.Dispatcher,
                        text => events.Publish(new ClientRuntimeEvent(ClientRuntimeEventKind.MissionTextDelta,
                            request.SessionId, Text: text)),
                        update => events.Publish(new ClientRuntimeEvent(ClientRuntimeEventKind.ToolCallStatus,
                            request.SessionId, ToolName: update.Call.Name, ToolStatus: update.State.ToString(),
                            ToolTarget: ExtractTarget(update.Call))), ct)
                    : await NewLocalSession(runtimeClient, session.Mission).SendAsync(request.Prompt,
                        session.Workspace.Capabilities, session.Workspace.Dispatcher,
                        text => events.Publish(new ClientRuntimeEvent(ClientRuntimeEventKind.MissionTextDelta,
                            request.SessionId, Text: text)),
                        update => events.Publish(new ClientRuntimeEvent(ClientRuntimeEventKind.ToolCallStatus,
                            request.SessionId, ToolName: update.Call.Name, ToolStatus: update.State.ToString(),
                            ToolTarget: ExtractTarget(update.Call))), ct);
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
                var json = JsonSerializer.Serialize(message, ConversationRelayJsonContext.Default.ClientRuntimeEvent);
                await context.Response.WriteAsync($"data: {json}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);
            }
        });
    }

    internal static bool UsesCloudMissionRuntime(string? mode) =>
        mode is null || mode.Equals("cloud", StringComparison.OrdinalIgnoreCase);

    // A null session Mission (no picker selection yet) falls through to each session type's own
    // constructor default ("vanilla" cloud-side, "ChatGPT" locally) rather than duplicating that
    // literal here.
    private static CloudMissionRuntimeSession NewCloudSession(HttpClient client, string? mission) =>
        mission is null ? new CloudMissionRuntimeSession(client) : new CloudMissionRuntimeSession(client, mission);

    private static MissionRuntimeSession NewLocalSession(HttpClient client, string? mission) =>
        mission is null ? new MissionRuntimeSession(client) : new MissionRuntimeSession(client, mission);

    // Read/Edit/Write carry a file_path argument, Bash carries command — either makes a fitting
    // "Read Foo.cs" / "Running ls -la" indicator target; unrecognized tools show no target.
    private static string? ExtractTarget(Microsoft.Extensions.AI.FunctionCallContent call)
    {
        var key = call.Name switch
        {
            "Read" or "Edit" or "Write" => "file_path",
            "Bash" => "command",
            _ => null,
        };
        return key is not null && call.Arguments?.TryGetValue(key, out var value) is true
            ? value?.ToString()
            : null;
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
