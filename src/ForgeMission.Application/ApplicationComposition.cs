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

/// <summary>The Missions landing's product actions (45.3 task 3A). Each answers with rendered
/// facts and typed failures only: no Project home, manifest, durable launch, Host client, Bob
/// handle, tool declaration, or attachment detail crosses this boundary, so a TUI can invoke the
/// same three with the same authorization, outcome, and failure.</summary>
public interface IMissionConversationService
{
    Task<ListMissionConversationsResponse> ListAsync(ListMissionConversationsRequest request, CancellationToken ct);
    Task<ListApprovedMissionVersionsResponse> ListApprovedVersionsAsync(ListApprovedMissionVersionsRequest request, CancellationToken ct);
    Task<CreateMissionConversationResponse> CreateAsync(CreateMissionConversationRequest request, CancellationToken ct);
}

/// <summary>Authoring's product actions (45.4 minimum): draft, promote, case, evaluate, publish.
/// Projects keeps the lifecycle and the publish guard; this boundary carries rendered facts and
/// typed failures only, so a TUI reaches the same outcomes.</summary>
public interface IMissionAuthoringService
{
    Task<GetMissionAuthoringResponse> GetAsync(GetMissionAuthoringRequest request, CancellationToken ct);
    Task<MissionAuthoringMutationResponse> CreateDraftAsync(CreateMissionDraftRequest request, CancellationToken ct);
    Task<MissionAuthoringMutationResponse> SaveDraftAsync(SaveMissionDraftRequest request, CancellationToken ct);
    Task<MissionAuthoringMutationResponse> PromoteCandidateAsync(PromoteMissionCandidateRequest request, CancellationToken ct);
    Task<MissionAuthoringMutationResponse> AddCaseAsync(AddEvaluationCaseRequest request, CancellationToken ct);
    Task<MissionAuthoringMutationResponse> UpdateCaseAsync(UpdateEvaluationCaseRequest request, CancellationToken ct);
    Task<MissionAuthoringMutationResponse> RunCaseAsync(RunEvaluationCaseRequest request, CancellationToken ct);
    Task<MissionAuthoringMutationResponse> PublishAsync(PublishMissionVersionRequest request, CancellationToken ct);
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
        var missionConversations = new MissionConversationService(missionVersions, clients, _sessions);
        var missionAuthoring = new MissionAuthoringService(projects, missionVersions, missionConversations, _sessions);
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
        MissionAuthoring = missionAuthoring;
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

    public IMissionAuthoringService MissionAuthoring { get; }

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
