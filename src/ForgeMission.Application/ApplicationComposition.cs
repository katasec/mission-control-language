using ForgeMission.Application.Transport;
using ForgeMission.ClientRuntime;
using ForgeMission.Core.Runtime;
using ForgeMission.Core.Tools;

namespace ForgeMission.Application;

public interface IProjectService
{
    Task<ProjectDraftResponse> DraftAsync(ProjectDraftRequest request, CancellationToken ct);

    Task<ProjectOperationResponse> CreateAsync(ProjectCreateRequest request, CancellationToken ct);

    Task<ProjectOperationResponse> OpenAsync(ProjectOpenRequest request, CancellationToken ct);

    Task<SelectProjectMissionResponse> SelectMissionAsync(SelectProjectMissionRequest request, CancellationToken ct);
}

public interface IApplicationSessionService
{
    Task<SessionSetupResponse> ReplaceAsync(SessionSetupRequest request, CancellationToken ct);
}

public interface IMissionSubmissionService
{
    Task<ProjectSubmissionResponse> StartAsync(StartProjectMissionRunRequest request, CancellationToken ct);

    Task<ProjectSubmissionResponse> RetryAsync(RetryProjectMissionSubmissionRequest request, CancellationToken ct);
}

public interface IRunHistoryService
{
    Task<GetProjectMissionStateResponse> GetStateAsync(GetProjectMissionStateRequest request, CancellationToken ct);

    Task<GetProjectRunsResponse> GetRunsAsync(GetProjectRunsRequest request, CancellationToken ct);

    Task<GetProjectRunResponse> GetRunAsync(GetProjectRunRequest request, CancellationToken ct);

    Task<GetProjectRunEventsResponse> GetEventsAsync(GetProjectRunEventsRequest request, CancellationToken ct);
}

public interface IProjectContentService
{
    Task<GetProjectWorkbenchResponse> GetWorkbenchAsync(GetProjectWorkbenchRequest request, CancellationToken ct);

    Task<OpenProjectDocumentResponse> OpenDocumentAsync(OpenProjectDocumentRequest request, CancellationToken ct);
}

public interface IConversationService
{
    Task<PromptResponse> PromptAsync(PromptRequest request, CancellationToken ct);
}

public interface ICapabilityActionService
{
    Task<CapabilityDispatchResponse> DispatchAsync(CapabilityDispatchRequest request, CancellationToken ct);
}

public interface IInteractionService
{
    ConfirmationResponse Respond(ConfirmationResponseRequest request);
}

public sealed class ApplicationComposition : IAsyncDisposable
{
    private readonly ApplicationSessionService _sessions;

    private ApplicationComposition(
        IHttpClientFactory clients,
        string? missionRuntimeMode,
        CapabilityAuthorizationPolicy policy,
        Action<ApplicationEvent> publish,
        CancellationToken applicationStopping)
    {
        _sessions = new ApplicationSessionService(policy, publish, applicationStopping);

        var projects = new ProjectService(_sessions);
        var submissions = new MissionSubmissionService(projects, clients, _sessions);
        var content = new ProjectContentService(projects, _sessions);
        var history = new ProjectMissionHistoryEndpointService(_sessions, projects, clients, publish, applicationStopping);
        var actions = CreateInteractionServices(clients, missionRuntimeMode, publish, applicationStopping);
        Projects = projects;
        Sessions = _sessions;
        MissionSubmissions = submissions;
        RunHistory = history;
        ProjectContent = content;
        Conversations = actions;
        Capabilities = actions;
        Interactions = actions;
    }

    public IProjectService Projects { get; }

    public IApplicationSessionService Sessions { get; }

    public IMissionSubmissionService MissionSubmissions { get; }

    public IRunHistoryService RunHistory { get; }

    public IProjectContentService ProjectContent { get; }

    public IConversationService Conversations { get; }

    public ICapabilityActionService Capabilities { get; }

    public IInteractionService Interactions { get; }

    public static ApplicationComposition Create(
        IHttpClientFactory clients,
        string? missionRuntimeMode,
        CapabilityAuthorizationPolicy policy,
        Action<ApplicationEvent> publish,
        CancellationToken applicationStopping) =>
        new(clients, missionRuntimeMode, policy, publish, applicationStopping);

    public ValueTask DisposeAsync() => _sessions.DisposeAsync();

    private ApplicationInteractionServices CreateInteractionServices(
        IHttpClientFactory clients,
        string? missionRuntimeMode,
        Action<ApplicationEvent> publish,
        CancellationToken applicationStopping)
    {
        return new ApplicationInteractionServices(
            _sessions,
            clients,
            missionRuntimeMode,
            publish,
            applicationStopping);
    }
}

internal sealed class ApplicationInteractionServices(
    ApplicationSessionService sessions,
    IHttpClientFactory clients,
    string? missionRuntimeMode,
    Action<ApplicationEvent> publish,
    CancellationToken applicationStopping)
    : IConversationService,
      ICapabilityActionService,
      IInteractionService
{

    public async Task<CapabilityDispatchResponse> DispatchAsync(
        CapabilityDispatchRequest request,
        CancellationToken ct)
    {
        var session = GetSession(request.SessionId);
        var capability = ToCapabilityRequest(request.Request)
            ?? throw new ArgumentException("Unsupported capability request.");
        var target = request.Request.FilePath ?? request.Request.Command;

        PublishToolStatus(request.SessionId, request.Request, "running", target);
        var result = await session.Execution.DispatchAsync(request.Request.CapabilityName, capability, ct);
        PublishToolStatus(request.SessionId, request.Request, result.IsError ? "error" : "done", target);
        return new CapabilityDispatchResponse(result.Content, result.IsError);
    }

    public ConfirmationResponse Respond(ConfirmationResponseRequest request)
    {
        var accepted = sessions.TryGet(request.SessionId, out var session) &&
                       session is not null &&
                       session.Confirmation.Resolve(request.ConfirmationId, request.Approved);
        return new ConfirmationResponse(accepted);
    }

    public async Task<PromptResponse> PromptAsync(PromptRequest request, CancellationToken ct)
    {
        var session = GetSession(request.SessionId);
        try
        {
            return session.Runtime == SessionRuntimeKind.DurableConversation
                ? await SendDurablePromptAsync(session, request, ct)
                : await SendMissionPromptAsync(session, request, ct);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            publish(new ApplicationEvent(ApplicationEventKind.Error, request.SessionId, Error: exception.Message));
            return new PromptResponse(exception.Message, IsError: true);
        }
    }

    private async Task<PromptResponse> SendDurablePromptAsync(
        ApplicationSession session,
        PromptRequest request,
        CancellationToken ct)
    {
        var conversationId = await session.Conversation.SendPromptAsync(
            () => CreateConversationSession(session, request.SessionId),
            request.Prompt,
            ct);
        return new PromptResponse(string.Empty, ConversationId: conversationId);
    }

    private async Task<PromptResponse> SendMissionPromptAsync(
        ApplicationSession session,
        PromptRequest request,
        CancellationToken ct)
    {
        var client = clients.CreateClient("mission-runtime");
        var publishText = (string text) =>
            publish(new ApplicationEvent(ApplicationEventKind.MissionTextDelta, request.SessionId, Text: text));
        var publishTool = (Microsoft.Extensions.AI.FunctionCallContent call, ToolCallNotificationState state) =>
            publish(new ApplicationEvent(
                ApplicationEventKind.ToolCallStatus,
                request.SessionId,
                ToolName: call.Name,
                ToolStatus: state.ToString(),
                ToolTarget: ExtractTarget(call)));

        var answer = UsesCloudMissionRuntime(missionRuntimeMode)
            ? await NewCloudSession(client, session.Mission).SendAsync(
                request.Prompt,
                session.Execution.Capabilities,
                session.Execution,
                publishText,
                update => publishTool(update.Call, update.State),
                ct)
            : await NewLocalSession(client, session.Mission).SendAsync(
                request.Prompt,
                session.Execution.Capabilities,
                session.Execution,
                publishText,
                update => publishTool(update.Call, update.State),
                ct);
        return new PromptResponse(answer);
    }

    private ConversationRuntimeSession CreateConversationSession(ApplicationSession session, string sessionId) =>
        new(
            sessionId,
            session.Mission ?? "Janus",
            new ConversationHostClient(clients.CreateClient("conversation-host")),
            session.Execution.Capabilities,
            session.Execution,
            publish,
            applicationStopping);

    private ApplicationSession GetSession(string sessionId)
    {
        if (!sessions.TryGet(sessionId, out var session) || session is null)
            throw new KeyNotFoundException();

        return session;
    }

    private void PublishToolStatus(
        string sessionId,
        CapabilityRequestData request,
        string status,
        string? target) =>
        publish(new ApplicationEvent(
            ApplicationEventKind.ToolCallStatus,
            sessionId,
            ToolName: request.Operation.ToString(),
            ToolStatus: status,
            ToolTarget: target));

    internal static bool UsesCloudMissionRuntime(string? mode) =>
        mode is null || mode.Equals("cloud", StringComparison.OrdinalIgnoreCase);

    private static CloudMissionRuntimeSession NewCloudSession(HttpClient client, string? mission) =>
        mission is null ? new CloudMissionRuntimeSession(client) : new CloudMissionRuntimeSession(client, mission);

    private static MissionRuntimeSession NewLocalSession(HttpClient client, string? mission) =>
        mission is null ? new MissionRuntimeSession(client) : new MissionRuntimeSession(client, mission);

    private static string? ExtractTarget(Microsoft.Extensions.AI.FunctionCallContent call) =>
        call.Name switch
        {
            "Read" or "Edit" or "Write" =>
                call.Arguments?.TryGetValue("file_path", out var file) is true ? file?.ToString() : null,
            "Bash" =>
                call.Arguments?.TryGetValue("command", out var command) is true ? command?.ToString() : null,
            _ => null,
        };

    private static ICapabilityRequest? ToCapabilityRequest(CapabilityRequestData request) =>
        request.Operation switch
        {
            CapabilityOperation.ReadFile when request.FilePath is not null =>
                new ReadFileCapabilityRequest(request.FilePath, request.Offset, request.Limit),
            CapabilityOperation.EditFile when request.FilePath is not null &&
                                                   request.OldString is not null &&
                                                   request.NewString is not null =>
                new EditFileCapabilityRequest(
                    request.FilePath,
                    request.OldString,
                    request.NewString,
                    request.ReplaceAll),
            CapabilityOperation.WriteFile when request.FilePath is not null && request.Content is not null =>
                new WriteFileCapabilityRequest(request.FilePath, request.Content),
            CapabilityOperation.ExecuteTerminal when request.Command is not null =>
                new ExecuteTerminalCapabilityRequest(request.Command),
            _ => null,
        };
}
