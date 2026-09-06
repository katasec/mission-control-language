using ForgeMission.Application.Transport;

namespace ForgeMission.Application;

/// <summary>Application owner for session-bound Project Mission history reads.</summary>
/// <remarks>The session scope owns the bounded reader and observation lifetime; this owner resolves
/// the live attachment and invokes that scope without becoming a second session store.</remarks>
internal sealed class RunHistoryService(
    ApplicationSessionService sessions,
    ProjectService projects,
    IHttpClientFactory clients,
    Action<ApplicationEvent> publish,
    CancellationToken applicationStopping) : IRunHistoryService
{
    public Task<GetProjectMissionStateResponse> GetStateAsync(GetProjectMissionStateRequest request, CancellationToken ct) =>
        ReadAsync(request.SessionId, read => read.GetStateAsync(ct), ct);

    public Task<GetProjectRunsResponse> GetRunsAsync(GetProjectRunsRequest request, CancellationToken ct) =>
        ReadAsync(request.SessionId, read => read.GetRunsAsync(request.Cursor, ct), ct);

    public Task<GetProjectRunResponse> GetRunAsync(GetProjectRunRequest request, CancellationToken ct) =>
        ReadAsync(request.SessionId, read => read.GetRunAsync(request.RunId, ct), ct);

    public Task<GetProjectRunEventsResponse> GetEventsAsync(GetProjectRunEventsRequest request, CancellationToken ct) =>
        ReadAsync(request.SessionId, read => read.GetEventsAsync(request.RunId, request.AfterSequence, request.ThroughSequence, ct), ct);

    private Task<T> ReadAsync<T>(string sessionId, Func<ProjectMissionReadScope, Task<T>> operation, CancellationToken ct)
    {
        if (!sessions.TryGet(sessionId, out var session) || session is null) throw new KeyNotFoundException();
        return session.ProjectMission.InvokeAsync(
            () => new ProjectMissionReadScope(session.Id, session.ProjectHome, projects,
                new ConversationHostClient(clients.CreateClient("conversation-host")), publish, applicationStopping),
            operation, ct);
    }
}
