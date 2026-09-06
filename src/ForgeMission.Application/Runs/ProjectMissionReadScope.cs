using ForgeMission.Application.Transport;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Application;

/// <summary>Small session-scoped composition object for one history reader, observation tail,
/// and required Project Mission refusal protocol. It never reaches Bob.</summary>
internal sealed class ProjectMissionReadScope : IAsyncDisposable
{
    private readonly RunObservationService _observation;
    private readonly RunHistoryService _history;
    private readonly ProjectMissionToolRefusal _refusal;

    public ProjectMissionReadScope(string sessionId, string home, ProjectService projects,
        ConversationHostClient host, Action<ApplicationEvent> publish, CancellationToken applicationStopping)
    {
        _refusal = new ProjectMissionToolRefusal(host);
        _observation = new RunObservationService(sessionId, home, projects, host, publish, _refusal.ApplyAsync, applicationStopping);
        _history = new RunHistoryService(home, projects, host, _observation);
    }

    public Task<GetProjectMissionStateResponse> GetStateAsync(CancellationToken ct) => _history.GetStateAsync(ct);

    public Task<GetProjectRunsResponse> GetRunsAsync(ProjectRunCursor? cursor, CancellationToken ct) =>
        _history.GetRunsAsync(cursor, ct);

    public Task<GetProjectRunResponse> GetRunAsync(Guid runId, CancellationToken ct) => _history.GetRunAsync(runId, ct);

    public Task<GetProjectRunEventsResponse> GetEventsAsync(Guid runId, long after, long? through, CancellationToken ct) =>
        _history.GetEventsAsync(runId, after, through, ct);

    public ValueTask DisposeAsync() => _observation.DisposeAsync();
}

/// <summary>Session-scoped lifecycle guard for the bounded Project Mission read owner.</summary>
internal sealed class ProjectMissionReadScopeSlot : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _disposeGate = new();
    private ProjectMissionReadScope? _session;
    private Task? _dispose;
    private bool _closed;

    public async Task<TResult> InvokeAsync<TResult>(Func<ProjectMissionReadScope> factory,
        Func<ProjectMissionReadScope, Task<TResult>> operation, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_closed) throw new InvalidOperationException("This Client Runtime session has been replaced.");
            _session ??= factory();
            return await operation(_session);
        }
        finally { _gate.Release(); }
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposeGate)
        {
            _dispose ??= DisposeCoreAsync();
            return new ValueTask(_dispose);
        }
    }

    private async Task DisposeCoreAsync()
    {
        ProjectMissionReadScope? session;
        await _gate.WaitAsync();
        try { if (_closed) return; _closed = true; session = _session; }
        finally { _gate.Release(); }
        if (session is not null) await session.DisposeAsync();
    }
}
