using System.Net;
using ForgeMission.Application.Transport;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Application;

/// <summary>Scoped verified Project Mission history reader. It reads durable pages and trace
/// bodies from Conversation Host while its containing scope owns the observation lifetime.</summary>
internal sealed class ProjectMissionHistoryReader
{
    private readonly string _home;
    private readonly ProjectService _projects;
    private readonly ConversationHostClient _host;
    private readonly RunObservationService _observation;

    public ProjectMissionHistoryReader(string home, ProjectService projects, ConversationHostClient host,
        RunObservationService observation)
    {
        _home = home;
        _projects = projects;
        _host = host;
        _observation = observation;
    }

    public async Task<GetProjectMissionStateResponse> GetStateAsync(CancellationToken ct)
    {
        ProjectRecord project;
        try { project = _projects.ReadForHome(_home); }
        catch (ProjectOperationException exception) { return new GetProjectMissionStateResponse(null, MissionSubmissionService.ToError(exception)); }

        var missions = Missions(project.Manifest);
        _observation.EnsureRefreshLoop();
        if (project.Manifest.SelectedMission is not { Origin: ProjectMissionOrigin.BuiltIn } selected || !MissionCatalog.IsAllowed(selected.Reference))
            return new GetProjectMissionStateResponse(new ProjectMissionState(missions,
                project.Manifest.Submission is null ? null : MissionSubmissionService.ToView(project.Manifest.Submission),
                null, null), MissionSubmissionService.Error(ProjectOperationErrorCode.UnknownMission,
                "This Project's selected Mission is unavailable."));

        var submission = project.Manifest.Submission is null ? null : MissionSubmissionService.ToView(project.Manifest.Submission);
        if (project.Manifest.ProjectMissionContainerId is not { } containerId)
            return new GetProjectMissionStateResponse(new ProjectMissionState(missions, submission,
                EmptyPage(), null), null);

        try
        {
            await _observation.EnsureTailAsync(project.Manifest, ct);
            await VerifyContainerAsync(project.Manifest, ct);
            var page = await _host.ReadProjectRunsAsync(containerId, null, null, ct);
            _observation.UpdateRefreshMode(project.Manifest, page);
            return new GetProjectMissionStateResponse(new ProjectMissionState(missions, submission, page,
                page.Synchronizing ? MissionSubmissionService.Error(ProjectOperationErrorCode.HistorySynchronizing,
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
            return new GetProjectRunsResponse(null, MissionSubmissionService.Error(ProjectOperationErrorCode.InvalidRunQuery,
                "The requested Project run page is invalid."));
        var project = ReadProject(out var error);
        if (project is null) return new GetProjectRunsResponse(null, error);
        if (project.Manifest.ProjectMissionContainerId is not { } containerId)
            return new GetProjectRunsResponse(EmptyPage(), null);
        try
        {
            await _observation.EnsureTailAsync(project.Manifest, ct);
            await VerifyContainerAsync(project.Manifest, ct);
            var page = await _host.ReadProjectRunsAsync(containerId,
                cursor?.AnchorSequence, cursor?.BeforeAcceptedSequence, ct);
            _observation.UpdateRefreshMode(project.Manifest, page);
            return new GetProjectRunsResponse(page, null);
        }
        catch (Exception exception) { return new GetProjectRunsResponse(null, ToHistoryError(exception)); }
    }

    public async Task<GetProjectRunResponse> GetRunAsync(Guid runId, CancellationToken ct)
    {
        if (runId == Guid.Empty)
            return new GetProjectRunResponse(null, MissionSubmissionService.Error(ProjectOperationErrorCode.InvalidRunQuery,
                "A Project run id is required."));
        var project = ReadProject(out var error);
        if (project?.Manifest.ProjectMissionContainerId is not { } containerId)
            return new GetProjectRunResponse(null, error ?? MissionSubmissionService.Error(ProjectOperationErrorCode.MissionRunNotFound,
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
            return new GetProjectRunEventsResponse(null, MissionSubmissionService.Error(ProjectOperationErrorCode.InvalidRunQuery,
                "The requested Project run trace is invalid."));
        var project = ReadProject(out var error);
        if (project?.Manifest.ProjectMissionContainerId is not { } containerId)
            return new GetProjectRunEventsResponse(null, error ?? MissionSubmissionService.Error(ProjectOperationErrorCode.MissionRunNotFound,
                "This Project has no Mission runs."));
        try
        {
            await VerifyContainerAsync(project.Manifest, ct);
            return new GetProjectRunEventsResponse(await _host.ReadProjectRunEventsAsync(containerId, runId, after, through, ct), null);
        }
        catch (Exception exception) { return new GetProjectRunEventsResponse(null, ToHistoryError(exception)); }
    }

    private ProjectRecord? ReadProject(out ProjectOperationError? error)
    {
        try { error = null; return _projects.ReadForHome(_home); }
        catch (ProjectOperationException exception) { error = MissionSubmissionService.ToError(exception); return null; }
    }

    private async Task VerifyContainerAsync(ProjectManifest manifest, CancellationToken ct)
    {
        if (manifest.ProjectMissionContainerId is not { } containerId) return;
        var snapshot = (await _host.ReadConversationAsync(containerId, ct)).Snapshot;
        if (snapshot.ConversationId != containerId || snapshot.Purpose != ConversationPurpose.ProjectMission ||
            snapshot.ProjectId != manifest.ProjectId)
            throw new ProjectOperationException(ProjectOperationErrorCode.MissionRunConflict,
                "The Project Mission container belongs to another Project.");
    }

    private static ProjectMissionsView Missions(ProjectManifest manifest) => new(
        MissionCatalog.All,
        manifest.SelectedMission is { Origin: ProjectMissionOrigin.BuiltIn } selected && MissionCatalog.IsAllowed(selected.Reference)
            ? selected.Reference : null,
        manifest.LegacyProjectControlConversationId is not null || manifest.MissionControlConversationId is not null);

    private static ProjectRunPage EmptyPage() => new(Guid.Empty, 0, 0, false, [], null);
    private static ProjectOperationError ToHistoryError(Exception exception) => exception switch
    {
        ConversationHostProjectException { StatusCode: HttpStatusCode.NotFound } => MissionSubmissionService.Error(
            ProjectOperationErrorCode.MissionRunNotFound, "The requested Project Mission run was not found."),
        ConversationHostProjectException => MissionSubmissionService.Error(ProjectOperationErrorCode.MissionRunConflict,
            "The Project Mission history conflicts with the current Project."),
        ConversationHostProtocolException => MissionSubmissionService.Error(ProjectOperationErrorCode.HistoryInvalid,
            "Forge received an invalid Project Mission history response."),
        _ => MissionSubmissionService.Error(ProjectOperationErrorCode.HistoryUnavailable,
            "Forge could not read Project Mission history."),
    };
}
