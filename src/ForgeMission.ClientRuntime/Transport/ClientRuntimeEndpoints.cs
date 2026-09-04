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
        // Replacement only (43.20 task 1): this route can never establish a Project's first
        // session or root. The store rejects a replacement that does not name the current session
        // or that changes its root, and the rejection is a 400 rather than a rendered outcome —
        // no correct surface, Desktop or TUI, ever sends one.
        app.MapPost("/transport/session/setup", async (SessionSetupRequest request, ClientRuntimeSessionStore sessions) =>
        {
            try
            {
                var session = await sessions.ReplaceAsync(
                    request.ReplacesSessionId, request.WorkspaceRoot, request.Mission, request.Runtime);
                return Results.Ok(new SessionSetupResponse(session.Id,
                    session.Workspace.Capabilities?.AvailableCapabilities ?? []));
            }
            catch (SessionReplacementRejectedException exception)
            {
                return Results.BadRequest(exception.Message);
            }
        });

        // Side-effect free: it answers "what would you name and where would you put it" and
        // nothing else. Create recomputes the same derivation and owns the collision-safe write.
        app.MapPost("/transport/project/draft", (ProjectDraftRequest request, ProjectStore projects) =>
        {
            try
            {
                return Results.Ok(new ProjectDraftResponse(
                    projects.Draft(request.Goal, request.TitleOverride, request.HomeOverride), Error: null));
            }
            catch (ProjectOperationException exception)
            {
                return Results.Ok(new ProjectDraftResponse(Draft: null, ToError(exception)));
            }
        });

        app.MapPost("/transport/project/create", (ProjectCreateRequest request,
            ProjectStore projects, ClientRuntimeSessionStore sessions) =>
        {
            try
            {
                var project = projects.Create(request.Goal, request.Title, request.HomePath);
                return Results.Ok(new ProjectOperationResponse(ProjectOperationOutcome.Created,
                    OpenSession(sessions, project, request.Mission, request.Runtime)));
            }
            catch (ProjectOperationException exception)
            {
                return Results.Ok(Failed(exception));
            }
        });

        app.MapPost("/transport/project/open", (ProjectOpenRequest request,
            ProjectStore projects, ClientRuntimeSessionStore sessions) =>
        {
            try
            {
                var result = projects.Open(request.HomePath);
                return Results.Ok(result.Project is { } project
                    ? new ProjectOperationResponse(ProjectOperationOutcome.Opened,
                        OpenSession(sessions, project, request.Mission, request.Runtime))
                    : new ProjectOperationResponse(ProjectOperationOutcome.GoalRequired,
                        Proposal: result.GoalRequired));
            }
            catch (ProjectOperationException exception)
            {
                return Results.Ok(Failed(exception));
            }
        });

        // 43.20 task 3. Both routes take only the session — and, to open, an entry ID this Runtime
        // itself minted. The Project home comes from the session's own root, never from the
        // caller, so no surface can name a Project or a path it did not already have open.
        app.MapPost("/transport/project/workbench", (
            GetProjectWorkbenchRequest request,
            ClientRuntimeSessionStore sessions,
            ProjectWorkbenchProjector workbench) =>
        {
            if (!sessions.TryGet(request.SessionId, out var session) || session?.Workspace.Root is not { } home)
                return Results.NotFound();

            try
            {
                return Results.Ok(new GetProjectWorkbenchResponse(workbench.Project(home)));
            }
            catch (ProjectOperationException exception)
            {
                return Results.Ok(new GetProjectWorkbenchResponse(null, ToError(exception)));
            }
        });

        app.MapPost("/transport/project/document", (
            OpenProjectDocumentRequest request,
            ClientRuntimeSessionStore sessions,
            ProjectWorkbenchProjector workbench) =>
        {
            if (!sessions.TryGet(request.SessionId, out var session) || session?.Workspace.Root is not { } home)
                return Results.NotFound();

            try
            {
                return Results.Ok(new OpenProjectDocumentResponse(workbench.OpenDocument(home, request.EntryId)));
            }
            catch (ProjectOperationException exception)
            {
                return Results.Ok(new OpenProjectDocumentResponse(null, ToError(exception)));
            }
        });

        // 43.21 task 1 — the universal Project Mission pair. Both take only the session and, to
        // run, a command id and the person's instruction. The mission comes from the persisted
        // selection and the Project goal from pinned Host state, so no surface can choose either.
        app.MapPost("/transport/project/mission/select", (
            SelectProjectMissionRequest request,
            ClientRuntimeSessionStore sessions,
            ProjectStore projects) =>
        {
            if (!sessions.TryGet(request.SessionId, out var session) || session?.Workspace.Root is not { } home)
                return Results.NotFound();

            try
            {
                // Deliberately does NOT go through the session slot: selection is manifest state,
                // not conversation state, so it must work before the container has ever been
                // opened — a person picks a mission and then runs it, in that order.
                var selected = projects.SelectMissionFor(home, request.Mission);
                return Results.Ok(new SelectProjectMissionResponse(selected.Reference));
            }
            catch (ProjectOperationException exception)
            {
                return Results.Ok(new SelectProjectMissionResponse(null, ToError(exception)));
            }
        });

        app.MapPost("/transport/project/mission/run", async (
            StartProjectMissionRunRequest request,
            ClientRuntimeSessionStore sessions,
            ProjectStore projects,
            IHttpClientFactory clients,
            ClientRuntimeEventHub events,
            IHostApplicationLifetime lifetime,
            CancellationToken ct) =>
        {
            if (!sessions.TryGet(request.SessionId, out var session) || session?.Workspace.Root is null)
                return Results.NotFound();

            try
            {
                // Refused BEFORE the container is opened, so a blank or oversized instruction never
                // creates durable state on its way to being rejected.
                ProjectMissionRuntimeSession.ValidateSubmission(request.CommandId, request.Input);

                // Open-and-start are ONE serialized slot call for the same reason the durable Janus
                // prompt path is: a separate open-then-start could interleave with a concurrent
                // session replacement and leave a container tail the store no longer tracks.
                var accepted = await session.ProjectMission.InvokeAsync(
                    () => new ProjectMissionRuntimeSession(
                        request.SessionId,
                        session.Workspace.Root!,
                        projects,
                        new ConversationHostClient(clients.CreateClient("conversation-host")),
                        events.Publish,
                        lifetime.ApplicationStopping),
                    async missions =>
                    {
                        await missions.OpenAsync(ct);
                        return await missions.StartRunAsync(request.CommandId, request.Input, ct);
                    },
                    ct);

                return Results.Ok(new StartProjectMissionRunResponse(
                    accepted.RunId, accepted.Mission, accepted.AcceptedSequence, accepted.Status));
            }
            catch (Exception exception) when (ToMissionRunError(exception) is { } error)
            {
                return Results.Ok(new StartProjectMissionRunResponse(
                    null, null, 0, Conversations.Contracts.ConversationRunStatus.Failed, error));
            }
        });

        // 43.20 task 2. Both routes take only the session and what the person typed: the Runtime
        // resolves the Project from the session's own root and reads the manifest itself, so no
        // surface supplies a Project path, a conversation ID, or a project goal.
        app.MapPost("/transport/project/mission-control/open", async (
            OpenProjectMissionControlRequest request,
            ClientRuntimeSessionStore sessions,
            ProjectStore projects,
            IHttpClientFactory clients,
            ClientRuntimeEventHub events,
            IHostApplicationLifetime lifetime,
            CancellationToken ct) =>
        {
            if (!sessions.TryGet(request.SessionId, out var session) || session is null)
                return Results.NotFound();

            try
            {
                var conversationId = await session.MissionControl.InvokeAsync(
                    () => new ProjectControlRuntimeSession(
                        request.SessionId,
                        session.Workspace.Root!,
                        projects,
                        new ConversationHostClient(clients.CreateClient("conversation-host")),
                        events.Publish,
                        lifetime.ApplicationStopping),
                    control => control.OpenAsync(ct),
                    ct);

                return Results.Ok(new OpenProjectMissionControlResponse(conversationId));
            }
            catch (Exception exception) when (ToMissionControlError(exception) is { } error)
            {
                return Results.Ok(new OpenProjectMissionControlResponse(null, error));
            }
        });

        app.MapPost("/transport/project/mission-control/submit", async (
            SubmitProjectMissionControlTurnRequest request,
            ClientRuntimeSessionStore sessions,
            CancellationToken ct) =>
        {
            if (!sessions.TryGet(request.SessionId, out var session) || session is null)
                return Results.NotFound();

            try
            {
                var accepted = await session.MissionControl.InvokeOpenedAsync(
                    control => control.SubmitAsync(request.CommandId, request.Text, ct),
                    () => new MissionControlNotOpenedException(), ct);

                return Results.Ok(new SubmitProjectMissionControlTurnResponse(
                    accepted.ConversationId, accepted.AcceptedSequence));
            }
            catch (Exception exception) when (ToMissionControlError(exception) is { } error)
            {
                return Results.Ok(new SubmitProjectMissionControlTurnResponse(null, 0, error));
            }
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

    // The Project home is the sole local execution root, and this is the only place a project
    // operation turns into a session — so a capability authority never exists before a Project does.
    private static ProjectSession OpenSession(
        ClientRuntimeSessionStore sessions, ProjectRecord project, string? mission, SessionRuntimeKind runtime)
    {
        var session = sessions.CreateForProject(project.Home, mission, runtime);
        return new ProjectSession(session.Id,
            session.Workspace.Capabilities?.AvailableCapabilities ?? [],
            new ProjectSummary(project.Manifest.ProjectId, project.Manifest.Title,
                project.Manifest.Goal, project.Home));
    }

    // Expected Project domain failures become one typed response every surface renders the same
    // way. Unexpected faults are deliberately not caught here: they fail the transport instead of
    // being laundered into a domain code.
    private static ProjectOperationResponse Failed(ProjectOperationException exception) =>
        new(ProjectOperationOutcome.Failed, Error: ToError(exception));

    private static ProjectOperationError ToError(ProjectOperationException exception) =>
        new(exception.Code, exception.Message);

    // The two EXPECTED failure sources for a Mission Control operation: a local Project/manifest
    // problem (including ManifestWriteFailed), and a Conversation-service rejection whose status
    // names a domain outcome. Anything else — a socket reset, an unexpected 5xx — returns null and
    // is left to fail the transport, rather than being laundered into a domain code.
    private static ProjectOperationError? ToMissionControlError(Exception exception) => exception switch
    {
        ProjectOperationException project => ToError(project),
        // Submitting before Mission Control has opened. Desktop's composer stays disabled until
        // it has, but a TUI could still order the two calls this way, and surface parity says both
        // get the same typed outcome rather than one getting an escaping transport failure.
        MissionControlNotOpenedException => new ProjectOperationError(
            ProjectOperationErrorCode.MissionControlInvalid,
            MissionControlMessage(ProjectOperationErrorCode.MissionControlInvalid)),
        HttpRequestException { StatusCode: { } status }
            when ProjectControlRuntimeSession.ToErrorCode(status) is { } code =>
            new ProjectOperationError(code, MissionControlMessage(code)),
        _ => null,
    };

    // The two EXPECTED failure sources for a Project Mission run: a local Project/manifest problem
    // (an unknown selection, blank or oversized input, a failed manifest write), and a
    // Conversation-service rejection whose status names a domain outcome. Anything else returns
    // null and is left to fail the transport rather than being laundered into a domain code.
    private static ProjectOperationError? ToMissionRunError(Exception exception) => exception switch
    {
        ProjectOperationException project => ToError(project),
        ProjectMissionNotOpenedException => new ProjectOperationError(
            ProjectOperationErrorCode.MissionRunNotFound,
            "This Project's missions are not open in this session."),
        // "One run at a time" and a genuine conflict share HTTP 409, so the Host's own reason text
        // is what separates them. Defaulting to the conflict rather than the busy state matters:
        // telling someone a run is in flight when it is not would be a lie the UI would act on.
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.Conflict } conflict
            when conflict.Message.Contains("already has an active mission run", StringComparison.Ordinal) =>
            new ProjectOperationError(
                ProjectOperationErrorCode.RunAlreadyActive,
                "This Project already has a mission run in progress. Wait for it to finish."),
        HttpRequestException { StatusCode: { } status }
            when ProjectMissionRuntimeSession.ToErrorCode(status) is { } code =>
            new ProjectOperationError(code, MissionRunMessage(code)),
        _ => null,
    };

    private static string MissionRunMessage(ProjectOperationErrorCode code) => code switch
    {
        ProjectOperationErrorCode.InvalidMissionInput => "That instruction could not be accepted.",
        ProjectOperationErrorCode.MissionRunNotFound => "This Project's missions could not be found.",
        ProjectOperationErrorCode.MissionRunConflict =>
            "That run conflicts with what is already recorded for this Project.",
        _ => "The mission run could not be started.",
    };

    // A rendered message, not the transport exception's own text: a person reading the surface
    // should not be shown "returned HTTP 409" or the name of an internal service. The typed code
    // carries the precise outcome; this carries what it means.
    private static string MissionControlMessage(ProjectOperationErrorCode code) => code switch
    {
        ProjectOperationErrorCode.MissionControlConflict =>
            "Mission Control could not accept that — it conflicts with what is already recorded for this Project.",
        ProjectOperationErrorCode.MissionControlNotFound =>
            "This Project's Mission Control conversation could not be found.",
        ProjectOperationErrorCode.MissionControlInvalid =>
            "Mission Control could not accept that message.",
        _ => "Mission Control is unavailable.",
    };

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
