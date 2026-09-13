using ForgeMission.Application.Transport;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Application;

/// <summary>Owns the scoped tail, refresh and invalidation delivery. Its required protocol hook
/// runs before ConversationTailReader advances a cursor.</summary>
internal sealed class RunObservationService : IAsyncDisposable
{
    private readonly string _sessionId;
    private readonly string _home;
    private readonly ProjectService _projects;
    private readonly ConversationHostClient _host;
    private readonly Action<ApplicationEvent> _publish;
    private readonly CancellationTokenSource _lifetime;
    private readonly Func<ConversationEvent, CancellationToken, Task> _requiredProtocol;
    private ConversationTailReader? _tail;
    private Guid? _container;
    private Task? _refresh;
    private bool _fast;
    private readonly object _invalidationGate = new();
    private bool _queued;
    private bool _dirty;
    private Guid _pendingContainer;
    private long _pendingSequence;
    private Task? _invalidation;

    public RunObservationService(string sessionId, string home, ProjectService projects, ConversationHostClient host,
        Action<ApplicationEvent> publish, Func<ConversationEvent, CancellationToken, Task> requiredProtocol, CancellationToken stopping)
    {
        _sessionId = sessionId; _home = home; _projects = projects; _host = host; _publish = publish;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(stopping); _requiredProtocol = requiredProtocol;
    }

    public void EnsureRefreshLoop() => _refresh ??= Task.Run(RefreshLoopAsync);

    public void UpdateRefreshMode(ProjectManifest manifest, ProjectRunPage page) => _fast =
        manifest.Submission?.Phase == ProjectSubmissionPhase.Prepared || page.Synchronizing ||
        page.Runs.Any(run => run.Status is ConversationRunStatus.Queued or ConversationRunStatus.Running or ConversationRunStatus.WaitingForTool);

    public async Task EnsureTailAsync(ProjectManifest manifest, CancellationToken ct)
    {
        if (manifest.ProjectMissionContainerId is not { } id || _container == id) return;
        if (_tail is not null) await _tail.DisposeAsync();
        _container = id; _tail = new ConversationTailReader(_sessionId, _host, _publish, _lifetime.Token, OnEventAsync);
        try { await _tail.StartAsync(id, 0, ct); }
        catch { await _tail.DisposeAsync(); _tail = null; _container = null; throw; }
    }

    private async Task OnEventAsync(ConversationEvent evt, CancellationToken ct)
    {
        await _requiredProtocol(evt, ct);
        if (_container is { } id) QueueInvalidation(id, evt.Sequence);
    }

    private async Task RefreshLoopAsync()
    {
        while (!_lifetime.IsCancellationRequested)
        {
            ProjectManifest? manifest = null; try { manifest = _projects.ReadForHome(_home).Manifest; } catch (ProjectOperationException) { }
            try { await Task.Delay(_fast || manifest?.Submission?.Phase == ProjectSubmissionPhase.Prepared ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(5), _lifetime.Token); }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { return; }
            if (manifest?.ProjectMissionContainerId is { } id) QueueInvalidation(id, manifest.Submission?.Acceptance?.AcceptedSequence ?? 0);
        }
    }

    private void QueueInvalidation(Guid container, long sequence)
    {
        lock (_invalidationGate)
        {
            if (_pendingContainer != container) { _pendingContainer = container; _pendingSequence = sequence; }
            else _pendingSequence = Math.Max(_pendingSequence, sequence);
            if (_queued) { _dirty = true; return; }
            _queued = true; _invalidation = PublishInvalidationsAsync();
        }
    }

    private async Task PublishInvalidationsAsync()
    {
        while (true)
        {
            await Task.Yield(); Guid container; long sequence;
            lock (_invalidationGate)
            {
                container = _pendingContainer; sequence = _pendingSequence;
                if (!_dirty) { _queued = false; _invalidation = null; }
                _dirty = false;
            }
            _publish(new ApplicationEvent(ApplicationEventKind.ProjectMissionChanged, _sessionId,
                ProjectMission: new ProjectMissionChange(container, sequence)));
            lock (_invalidationGate) if (!_queued) return;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync(); if (_tail is not null) await _tail.DisposeAsync(); if (_refresh is not null) await _refresh;
        Task? invalidation; lock (_invalidationGate) invalidation = _invalidation; if (invalidation is not null) await invalidation; _lifetime.Dispose();
    }
}
