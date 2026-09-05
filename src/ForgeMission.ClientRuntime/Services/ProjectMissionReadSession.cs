using System.Net;
using ForgeMission.ClientRuntime.Transport;
using ForgeMission.ClientRuntime.TransportHost;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ClientRuntime.Services;

/// <summary>
/// The read-side owner for one open Project. It has one bounded tail and forwards only an
/// invalidation hint; durable pages and trace bodies are always re-read from Conversation Host.
/// </summary>
internal sealed class ProjectMissionReadSession : IAsyncDisposable
{
    private readonly string _sessionId;
    private readonly string _home;
    private readonly ProjectStore _projects;
    private readonly ConversationHostClient _host;
    private readonly Action<ClientRuntimeEvent> _publish;
    private readonly CancellationToken _applicationStopping;
    private readonly CancellationTokenSource _refreshLifetime;
    private readonly ProjectMissionToolRefusal _toolRefusal;
    private readonly object _invalidationGate = new();
    private ConversationTailReader? _tail;
    private Guid? _tailedContainer;
    private Task? _refreshTask;
    private bool _fastRefresh;
    private bool _refreshQueued;
    private bool _refreshDirty;
    private Guid _pendingContainer;
    private long _pendingSequence;
    private Task? _invalidationTask;

    public ProjectMissionReadSession(string sessionId, string home, ProjectStore projects,
        ConversationHostClient host, Action<ClientRuntimeEvent> publish, CancellationToken applicationStopping)
    {
        _sessionId = sessionId;
        _home = home;
        _projects = projects;
        _host = host;
        _publish = publish;
        _applicationStopping = applicationStopping;
        _refreshLifetime = CancellationTokenSource.CreateLinkedTokenSource(applicationStopping);
        _toolRefusal = new ProjectMissionToolRefusal(host);
    }

    public async Task<GetProjectMissionStateResponse> GetStateAsync(CancellationToken ct)
    {
        ProjectRecord project;
        try { project = _projects.ReadForHome(_home); }
        catch (ProjectOperationException exception) { return new GetProjectMissionStateResponse(null, ProjectMissionApplication.ToError(exception)); }

        var missions = Missions(project.Manifest);
        EnsureRefreshLoop();
        if (project.Manifest.SelectedMission is not { Origin: ProjectMissionOrigin.BuiltIn } selected || !ProjectMissions.IsAllowed(selected.Reference))
            return new GetProjectMissionStateResponse(new ProjectMissionState(missions,
                project.Manifest.Submission is null ? null : ProjectMissionApplication.ToView(project.Manifest.Submission),
                null, null), ProjectMissionApplication.Error(ProjectOperationErrorCode.UnknownMission,
                "This Project's selected Mission is unavailable."));

        var submission = project.Manifest.Submission is null ? null : ProjectMissionApplication.ToView(project.Manifest.Submission);
        if (project.Manifest.ProjectMissionContainerId is not { } containerId)
            return new GetProjectMissionStateResponse(new ProjectMissionState(missions, submission,
                EmptyPage(), null), null);

        try
        {
            await EnsureTailAsync(project.Manifest, ct);
            await VerifyContainerAsync(project.Manifest, ct);
            var page = await _host.ReadProjectRunsAsync(containerId, null, null, ct);
            UpdateRefreshMode(project.Manifest, page);
            return new GetProjectMissionStateResponse(new ProjectMissionState(missions, submission, page,
                page.Synchronizing ? ProjectMissionApplication.Error(ProjectOperationErrorCode.HistorySynchronizing,
                    "Project run history is synchronizing.") : null), null);
        }
        catch (Exception exception)
        {
            return new GetProjectMissionStateResponse(new ProjectMissionState(missions, submission, null,
                ToHistoryError(exception)), null);
        }
    }

    public async Task<GetProjectRunsResponse> GetRunsAsync(ProjectRunCursor? cursor, CancellationToken ct)
    {
        if (cursor is { AnchorSequence: < 0 } || cursor is { BeforeAcceptedSequence: < 0 })
            return new GetProjectRunsResponse(null, ProjectMissionApplication.Error(ProjectOperationErrorCode.InvalidRunQuery,
                "The requested Project run page is invalid."));
        var project = ReadProject(out var error);
        if (project is null) return new GetProjectRunsResponse(null, error);
        if (project.Manifest.ProjectMissionContainerId is not { } containerId)
            return new GetProjectRunsResponse(EmptyPage(), null);
        try
        {
            await EnsureTailAsync(project.Manifest, ct);
            await VerifyContainerAsync(project.Manifest, ct);
            var page = await _host.ReadProjectRunsAsync(containerId,
                cursor?.AnchorSequence, cursor?.BeforeAcceptedSequence, ct);
            UpdateRefreshMode(project.Manifest, page);
            return new GetProjectRunsResponse(page, null);
        }
        catch (Exception exception) { return new GetProjectRunsResponse(null, ToHistoryError(exception)); }
    }

    public async Task<GetProjectRunResponse> GetRunAsync(Guid runId, CancellationToken ct)
    {
        if (runId == Guid.Empty)
            return new GetProjectRunResponse(null, ProjectMissionApplication.Error(ProjectOperationErrorCode.InvalidRunQuery,
                "A Project run id is required."));
        var project = ReadProject(out var error);
        if (project?.Manifest.ProjectMissionContainerId is not { } containerId)
            return new GetProjectRunResponse(null, error ?? ProjectMissionApplication.Error(ProjectOperationErrorCode.MissionRunNotFound,
                "This Project has no Mission runs."));
        try
        {
            await VerifyContainerAsync(project.Manifest, ct);
            return new GetProjectRunResponse(await _host.ReadProjectRunAsync(containerId, runId, ct), null);
        }
        catch (Exception exception) { return new GetProjectRunResponse(null, ToHistoryError(exception)); }
    }

    public async Task<GetProjectRunEventsResponse> GetEventsAsync(Guid runId, long after, long? through, CancellationToken ct)
    {
        if (runId == Guid.Empty || after < 0 || through is < 0 || through is { } end && end < after)
            return new GetProjectRunEventsResponse(null, ProjectMissionApplication.Error(ProjectOperationErrorCode.InvalidRunQuery,
                "The requested Project run trace is invalid."));
        var project = ReadProject(out var error);
        if (project?.Manifest.ProjectMissionContainerId is not { } containerId)
            return new GetProjectRunEventsResponse(null, error ?? ProjectMissionApplication.Error(ProjectOperationErrorCode.MissionRunNotFound,
                "This Project has no Mission runs."));
        try
        {
            await VerifyContainerAsync(project.Manifest, ct);
            return new GetProjectRunEventsResponse(await _host.ReadProjectRunEventsAsync(containerId, runId, after, through, ct), null);
        }
        catch (Exception exception) { return new GetProjectRunEventsResponse(null, ToHistoryError(exception)); }
    }

    public async Task EnsureTailAsync(ProjectManifest manifest, CancellationToken ct)
    {
        if (manifest.ProjectMissionContainerId is not { } containerId || _tailedContainer == containerId)
            return;
        if (_tail is not null)
            await _tail.DisposeAsync();
        _tailedContainer = containerId;
        _tail = new ConversationTailReader(_sessionId, _host, _publish, _applicationStopping, OnEventAsync);
        try
        {
            await _tail.StartAsync(containerId, afterSequence: 0, ct);
        }
        catch
        {
            // The initial stream can fail before it establishes a usable subscription. Do not
            // retain that faulted readiness signal: the next explicit state read owns a fresh
            // connection attempt and can recover after Host comes back.
            await _tail.DisposeAsync();
            _tail = null;
            _tailedContainer = null;
            throw;
        }
    }

    private void EnsureRefreshLoop()
    {
        _refreshTask ??= Task.Run(() => RefreshLoopAsync(_refreshLifetime.Token));
    }

    private async Task RefreshLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            ProjectManifest? manifest = null;
            try { manifest = _projects.ReadForHome(_home).Manifest; }
            catch (ProjectOperationException) { }

            var delay = _fastRefresh || manifest?.Submission?.Phase == ProjectSubmissionPhase.Prepared
                ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(5);
            try { await Task.Delay(delay, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }

            if (manifest?.ProjectMissionContainerId is { } container)
                QueueInvalidation(container, manifest.Submission?.Acceptance?.AcceptedSequence ?? 0);
        }
    }

    private async Task OnEventAsync(ConversationEvent evt, CancellationToken ct)
    {
        await _toolRefusal.ApplyAsync(evt, ct);
        if (_tailedContainer is { } containerId)
            QueueInvalidation(containerId, evt.Sequence);
    }

    // Event tails and fallback ticks may arrive together. One queued notification plus one dirty
    // replacement bounds work without inventing a global event queue; a later state read always
    // rehydrates the authoritative Host page.
    private void QueueInvalidation(Guid containerId, long sequence)
    {
        lock (_invalidationGate)
        {
            if (_pendingContainer != containerId)
            {
                _pendingContainer = containerId;
                _pendingSequence = sequence;
            }
            else
            {
                _pendingSequence = Math.Max(_pendingSequence, sequence);
            }
            if (_refreshQueued)
            {
                _refreshDirty = true;
                return;
            }
            _refreshQueued = true;
            _invalidationTask = PublishInvalidationsAsync();
        }
    }

    private async Task PublishInvalidationsAsync()
    {
        while (true)
        {
            await Task.Yield();
            Guid container;
            long sequence;
            lock (_invalidationGate)
            {
                container = _pendingContainer;
                sequence = _pendingSequence;
                if (!_refreshDirty)
                {
                    _refreshQueued = false;
                    _invalidationTask = null;
                }
                _refreshDirty = false;
            }
            _publish(new ClientRuntimeEvent(ClientRuntimeEventKind.ProjectMissionChanged, _sessionId,
                ProjectMission: new ProjectMissionChange(container, sequence)));
            lock (_invalidationGate)
            {
                if (!_refreshQueued)
                    return;
            }
        }
    }

    private ProjectRecord? ReadProject(out ProjectOperationError? error)
    {
        try { error = null; return _projects.ReadForHome(_home); }
        catch (ProjectOperationException exception) { error = ProjectMissionApplication.ToError(exception); return null; }
    }

    private async Task VerifyContainerAsync(ProjectManifest manifest, CancellationToken ct)
    {
        if (manifest.ProjectMissionContainerId is not { } containerId)
            return;
        var snapshot = (await _host.ReadConversationAsync(containerId, ct)).Snapshot;
        if (snapshot.ConversationId != containerId || snapshot.Purpose != ConversationPurpose.ProjectMission ||
            snapshot.ProjectId != manifest.ProjectId)
            throw new ProjectOperationException(ProjectOperationErrorCode.MissionRunConflict,
                "The Project Mission container belongs to another Project.");
    }

    private static ProjectMissionsView Missions(ProjectManifest manifest) => new(
        ProjectMissions.All,
        manifest.SelectedMission is { Origin: ProjectMissionOrigin.BuiltIn } selected && ProjectMissions.IsAllowed(selected.Reference)
            ? selected.Reference : null,
        manifest.LegacyProjectControlConversationId is not null || manifest.MissionControlConversationId is not null);

    private static ProjectRunPage EmptyPage() => new(Guid.Empty, 0, 0, false, [], null);
    private void UpdateRefreshMode(ProjectManifest manifest, ProjectRunPage page) =>
        _fastRefresh = manifest.Submission?.Phase == ProjectSubmissionPhase.Prepared || page.Synchronizing ||
            page.Runs.Any(run => run.Status is ConversationRunStatus.Queued or ConversationRunStatus.Running or ConversationRunStatus.WaitingForTool);
    private static ProjectOperationError ToHistoryError(Exception exception) => exception switch
    {
        ConversationHostProjectException { StatusCode: HttpStatusCode.NotFound } => ProjectMissionApplication.Error(
            ProjectOperationErrorCode.MissionRunNotFound, "The requested Project Mission run was not found."),
        ConversationHostProjectException => ProjectMissionApplication.Error(ProjectOperationErrorCode.MissionRunConflict,
            "The Project Mission history conflicts with the current Project."),
        ConversationHostProtocolException => ProjectMissionApplication.Error(ProjectOperationErrorCode.HistoryInvalid,
            "Forge received an invalid Project Mission history response."),
        _ => ProjectMissionApplication.Error(ProjectOperationErrorCode.HistoryUnavailable,
            "Forge could not read Project Mission history."),
    };

    public async ValueTask DisposeAsync()
    {
        await _refreshLifetime.CancelAsync();
        if (_tail is not null)
            await _tail.DisposeAsync();
        if (_refreshTask is not null)
            await _refreshTask;
        Task? invalidation;
        lock (_invalidationGate) invalidation = _invalidationTask;
        if (invalidation is not null)
            await invalidation;
        _refreshLifetime.Dispose();
    }
}

/// <summary>Session-scoped lifecycle guard for the bounded Project Mission read owner.</summary>
internal sealed class ProjectMissionReadSessionSlot : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ProjectMissionReadSession? _session;
    private bool _closed;

    public async Task<TResult> InvokeAsync<TResult>(Func<ProjectMissionReadSession> factory,
        Func<ProjectMissionReadSession, Task<TResult>> operation, CancellationToken ct)
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

    public async ValueTask DisposeAsync()
    {
        ProjectMissionReadSession? session;
        await _gate.WaitAsync();
        try { if (_closed) return; _closed = true; session = _session; }
        finally { _gate.Release(); }
        if (session is not null) await session.DisposeAsync();
    }
}
