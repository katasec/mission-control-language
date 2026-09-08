using ForgeMission.Application.Transport;
using ForgeMission.ClientRuntime;
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

public interface IMissionHandsConversationService
{
    Task<AcknowledgeMissionHandsResponse> AcknowledgeAsync(AcknowledgeMissionHandsRequest request, CancellationToken ct);
    Task<DetachMissionHandsResponse> DetachAsync(DetachMissionHandsRequest request, CancellationToken ct);
    Task<GetMissionHandsStatusResponse> GetStatusAsync(GetMissionHandsStatusRequest request, CancellationToken ct);
    Task<ExecuteMissionHandsResponse> ExecuteAsync(ExecuteMissionHandsRequest request, CancellationToken ct);
    Task<CancelMissionHandsResponse> CancelAsync(CancelMissionHandsRequest request, CancellationToken ct);
    Task<RecoverMissionHandsResponse> RecoverAsync(RecoverMissionHandsRequest request, CancellationToken ct);
}

public interface IMissionConversationService
{
    Task<ListApprovedMissionVersionsResponse> ListApprovedAsync(ListApprovedMissionVersionsRequest request, CancellationToken ct);
    Task<ListMissionConversationsResponse> ListAsync(ListMissionConversationsRequest request, CancellationToken ct);
    Task<CreateMissionConversationResponse> CreateAsync(CreateMissionConversationRequest request, CancellationToken ct);
    Task<GetMissionConversationResponse> GetAsync(GetMissionConversationRequest request, CancellationToken ct);
    Task<MissionTurnCommandResponse> SubmitAsync(SubmitMissionConversationTurnRequest request, CancellationToken ct);
    Task<MissionTurnCommandResponse> RetryAsync(RetryMissionConversationTurnRequest request, CancellationToken ct);
    Task<MissionTurnCommandResponse> CancelAsync(CancelMissionConversationTurnRequest request, CancellationToken ct);
    Task<GetMissionTurnTraceResponse> GetTraceAsync(GetMissionTurnTraceRequest request, CancellationToken ct);
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
        var missionVersions = new MissionVersionService(projects);
        var missionConversations = new MissionConversationService(missionVersions, _sessions, clients);
        var hands = new MissionHandsConversationService(projects, _sessions, clients, policy, applicationStopping);
        var submissions = new MissionSubmissionService(projects, clients, _sessions, hands);
        var content = new ProjectContentService(projects, _sessions);
        var history = new RunHistoryService(_sessions, projects, clients, publish, applicationStopping);
        var conversations = new ConversationService(_sessions, clients, missionRuntimeMode, publish, applicationStopping);
        var capabilities = new CapabilityActionService(_sessions, publish);
        var interactions = new InteractionService(_sessions);
        Projects = projects;
        MissionVersions = missionVersions;
        MissionConversations = missionConversations;
        Sessions = _sessions;
        MissionSubmissions = submissions;
        RunHistory = history;
        ProjectContent = content;
        Conversations = conversations;
        Capabilities = capabilities;
        Interactions = interactions;
        MissionHands = hands;
    }

    public IProjectService Projects { get; }

    // Surface-neutral owner interface. No Application.Transport DTO or route exists until 45.4.
    internal IMissionVersionService MissionVersions { get; }

    public IMissionConversationService MissionConversations { get; }

    public IApplicationSessionService Sessions { get; }

    public IMissionSubmissionService MissionSubmissions { get; }

    public IRunHistoryService RunHistory { get; }

    public IProjectContentService ProjectContent { get; }

    public IConversationService Conversations { get; }

    public ICapabilityActionService Capabilities { get; }

    public IInteractionService Interactions { get; }

    public IMissionHandsConversationService MissionHands { get; }

    public static ApplicationComposition Create(
        IHttpClientFactory clients,
        string? missionRuntimeMode,
        CapabilityAuthorizationPolicy policy,
        Action<ApplicationEvent> publish,
        CancellationToken applicationStopping) =>
        new(clients, missionRuntimeMode, policy, publish, applicationStopping);

    public ValueTask DisposeAsync() => _sessions.DisposeAsync();
}
