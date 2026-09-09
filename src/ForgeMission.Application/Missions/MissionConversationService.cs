using ForgeMission.Application.Transport;
using ForgeMission.Conversations.Contracts;
// Contracts and Transport each name a create request/response for this operation, because each
// owns one side of it: the durable command, and the surface action. Aliases keep both readable
// here rather than renaming a durable contract to suit a caller.
using HostCreateMissionConversationRequest = ForgeMission.Conversations.Contracts.CreateMissionConversationRequest;
using HostCreateMissionConversationResponse = ForgeMission.Conversations.Contracts.CreateMissionConversationResponse;
using HostListMissionConversationsResponse = ForgeMission.Conversations.Contracts.ListMissionConversationsResponse;
using SurfaceCreateMissionConversationRequest = ForgeMission.Application.Transport.CreateMissionConversationRequest;
using SurfaceCreateMissionConversationResponse = ForgeMission.Application.Transport.CreateMissionConversationResponse;
using SurfaceListMissionConversationsRequest = ForgeMission.Application.Transport.ListMissionConversationsRequest;
using SurfaceListMissionConversationsResponse = ForgeMission.Application.Transport.ListMissionConversationsResponse;

namespace ForgeMission.Application;

/// <summary>Coordinates Project-owned immutable values with the durable Host. It never writes a
/// manifest directly, sequences durable facts, or creates a local capability attachment.</summary>
internal sealed class MissionConversationService(
    MissionVersionService versions, IHttpClientFactory clients, ApplicationSessionService? sessions = null)
    : IMissionConversationService
{
    // ── Surface-facing projections (45.3 task 3A) ───────────────────────────────────────────
    // Host owns conversation identity, the pinned launch, and update order. Projects owns local
    // display identity and which version is approved. Neither borrows the other's answer, and a
    // caller supplies neither.

    public async Task<SurfaceListMissionConversationsResponse> ListAsync(
        SurfaceListMissionConversationsRequest request, CancellationToken ct)
    {
        try
        {
            var home = RequiredHome(request.SessionId);
            var projectId = versions.ReadProjectId(home, ct);
            var conversations = (await Host().ListMissionConversationsAsync(projectId, ct)).Conversations;
            var identities = await versions.ResolveVersionIdentitiesAsync(home,
                conversations.Select(item => item.Launch.MissionVersionId).Distinct().ToArray(), ct);
            var rows = conversations
                .OrderByDescending(item => item.UpdatedAtUtc)
                .Select(item => new MissionConversationListItem(item.ConversationId,
                    identities.TryGetValue(item.Launch.MissionVersionId, out var local) ? local.MissionName : null,
                    item.Launch.VersionNumber, item.UpdatedAtUtc))
                .ToArray();
            return new SurfaceListMissionConversationsResponse(rows, null);
        }
        catch (Exception exception) when (Unavailable(exception))
        {
            return new SurfaceListMissionConversationsResponse(null, ToError(exception, ProjectOperationErrorCode.HistoryUnavailable));
        }
    }

    public async Task<ListApprovedMissionVersionsResponse> ListApprovedVersionsAsync(
        ListApprovedMissionVersionsRequest request, CancellationToken ct)
    {
        try
        {
            var approved = await versions.ListApprovedVersionsAsync(RequiredHome(request.SessionId), ct);
            var options = approved
                .OrderBy(item => item.MissionName, StringComparer.OrdinalIgnoreCase)
                .Select(item => new ApprovedMissionVersionOption(item.MissionId, item.MissionName,
                    item.MissionVersionId, item.VersionNumber, item.DefinitionHash, item.Profile))
                .ToArray();
            return new ListApprovedMissionVersionsResponse(options, null);
        }
        catch (Exception exception) when (Unavailable(exception))
        {
            return new ListApprovedMissionVersionsResponse(null, ToError(exception, ProjectOperationErrorCode.HistoryUnavailable));
        }
    }

    /// <summary>One authorization read decides the version, immediately before Host admission.
    /// The caller's expected version is only ever a precondition on that answer: it can veto a
    /// create whose approved version has moved since the operator read it, and it can never
    /// select one.</summary>
    public async Task<SurfaceCreateMissionConversationResponse> CreateAsync(
        SurfaceCreateMissionConversationRequest request, CancellationToken ct)
    {
        try
        {
            var home = RequiredHome(request.SessionId);
            if (request.CommandId == Guid.Empty || request.MissionId == Guid.Empty || request.ExpectedMissionVersionId == Guid.Empty)
                return Failed(ProjectOperationErrorCode.InvalidMissionInput,
                    "A mission, the approved version you were shown, and a command id are required.");

            var provenance = await versions.ResolveActiveApprovedLaunchAsync(home, request.MissionId, ct);
            if (provenance.Launch.MissionVersionId != request.ExpectedMissionVersionId)
                return Failed(ProjectOperationErrorCode.VersionChanged,
                    "This mission's approved version changed while you were reading it. Review the current version before starting.");

            var created = await Host().CreateMissionConversationAsync(new HostCreateMissionConversationRequest(
                provenance.ProjectId, request.CommandId, provenance.Launch), ct);
            var pinned = created.Launch;
            return new SurfaceCreateMissionConversationResponse(new CreatedMissionConversation(created.ConversationId,
                new MissionAccessApproval(pinned.MissionVersionId, pinned.VersionNumber, pinned.DefinitionHash, pinned.Profile)), null);
        }
        catch (Exception exception) when (Expected(exception))
        {
            return new SurfaceCreateMissionConversationResponse(null, ToError(exception, ProjectOperationErrorCode.MissionRunConflict));
        }
    }

    // ── Existing durable coordination (45.2) ────────────────────────────────────────────────

    public async Task<HostCreateMissionConversationResponse> CreateAsync(string home, Guid missionId, Guid commandId, CancellationToken ct)
    {
        var provenance = await versions.ResolveActiveApprovedLaunchAsync(home, missionId, ct);
        return await Host().CreateMissionConversationAsync(new HostCreateMissionConversationRequest(
            provenance.ProjectId, commandId, provenance.Launch), ct);
    }

    public async Task<HostListMissionConversationsResponse> ListAsync(string home, CancellationToken ct)
    {
        var projectId = versions.ReadProjectId(home, ct);
        return await Host().ListMissionConversationsAsync(projectId, ct);
    }

    public Task<SubmitMissionTurnResponse> SubmitAsync(Guid conversationId, Guid commandId, string text, CancellationToken ct) =>
        Host().SubmitMissionTurnAsync(new SubmitMissionTurnRequest(conversationId, commandId, text), ct);

    public Task<SubmitMissionTurnResponse> RetryAsync(Guid conversationId, Guid turnId, Guid commandId, CancellationToken ct) =>
        Host().RetryMissionTurnAsync(new RetryMissionTurnRequest(conversationId, turnId, commandId), ct);

    public Task<CancelMissionTurnResponse> CancelAsync(Guid conversationId, Guid turnId, Guid turnAttemptId, Guid commandId, CancellationToken ct) =>
        Host().CancelMissionTurnAsync(new CancelMissionTurnRequest(conversationId, turnId, turnAttemptId, commandId), ct);

    public async Task<EvaluationResult> StartEvaluationAsync(string home, Guid missionId, Guid missionVersionId, Guid caseId, CancellationToken ct)
    {
        var pending = await versions.CreatePendingEvaluationAsync(home, missionId, missionVersionId, caseId, ct);
        var launch = new DurableMissionLaunch(pending.Version.MissionVersionId, pending.Version.VersionNumber,
            pending.Version.DefinitionHash, pending.Version.DefinitionText, pending.Version.CapabilityProfile, pending.Version.Package);
        var request = new StartEvaluationRequest(pending.ProjectId, pending.MissionId, pending.Version.MissionVersionId,
            pending.EvaluationCase.EvaluationCaseId, pending.Result.EvaluationResultId, pending.Version.CandidateRevision,
            pending.Version.DefinitionHash, launch, pending.EvaluationCase.Input);
        try
        {
            var response = await Host().StartEvaluationAsync(request, ct);
            return await ReconcileIfTerminalAsync(home, pending, response.Projection, ct);
        }
        catch (ConversationHostProjectException exception) when (exception.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.Conflict)
        {
            return await versions.ReconcilePendingCompletionAsync(home, missionId, missionVersionId, caseId, pending.Result.EvaluationResultId,
                pending.Version.CandidateRevision, pending.Version.DefinitionHash, EvaluationOutcome.Failed, exception.Error.Message, null, ct);
        }
    }

    public async Task<EvaluationResult?> ReconcileAsync(string home, Guid missionId, Guid missionVersionId, Guid caseId,
        Guid evaluationResultId, CancellationToken ct)
    {
        var projection = await Host().GetEvaluationAsync(evaluationResultId, ct);
        if (!projection.Projection.IsTerminal) return null;
        var pending = await versions.ReadPendingEvaluationAsync(home, missionId, missionVersionId, caseId, evaluationResultId, ct);
        return await ReconcileIfTerminalAsync(home, pending, projection.Projection, ct);
    }

    private async Task<EvaluationResult> ReconcileIfTerminalAsync(string home, PendingEvaluationAdmission pending,
        EvaluationProjection projection, CancellationToken ct)
    {
        if (!projection.IsTerminal) return pending.Result;
        var outcome = projection.ObservedOutcome == EvaluationOutcomeWire.Succeeded ? EvaluationOutcome.Succeeded : EvaluationOutcome.Failed;
        var trace = projection.TraceOrigin is { } origin ? new EvaluationTraceOrigin(origin.ConversationId, origin.TurnId, origin.TurnAttemptId) : null;
        return await versions.ReconcilePendingCompletionAsync(home, pending.MissionId, pending.Version.MissionVersionId,
            pending.EvaluationCase.EvaluationCaseId, pending.Result.EvaluationResultId, pending.Version.CandidateRevision,
            pending.Version.DefinitionHash, outcome, projection.ObservedOutputSummary ?? projection.Reason ?? "", trace, ct);
    }

    private ConversationHostClient Host() => new(clients.CreateClient("conversation-host"));

    // A stale or foreign session is not a Project outcome, so it is not laundered into a typed
    // ProjectOperationError. It leaves as the KeyNotFoundException every other session-scoped
    // owner throws, which the Host route already answers with 404.
    private string RequiredHome(string sessionId) =>
        sessions is not null && sessions.TryGet(sessionId, out var session) && session is not null
            ? session.ProjectHome
            : throw new KeyNotFoundException();

    private static SurfaceCreateMissionConversationResponse Failed(ProjectOperationErrorCode code, string message) =>
        new(null, new ProjectOperationError(code, message));

    // A Project rule and a Host refusal are both expected outcomes with a caller-meaningful
    // reason. Anything else stays an exception, because inventing a typed failure for an
    // unknown fault is how a surface ends up reporting a bug as a product state.
    private static bool Expected(Exception exception) =>
        exception is ProjectOperationException or ConversationHostProjectException;

    // A read may additionally fail because the Conversation Host is simply not reachable. For a
    // query that is an availability answer and the caller can honestly retry it. Deliberately NOT
    // applied to create: an unreachable Host there means the command's fate is unknown, and
    // reporting that as a definitive failure would invite a second create.
    private static bool Unavailable(Exception exception) =>
        Expected(exception) || exception is HttpRequestException;

    private static ProjectOperationError ToError(Exception exception, ProjectOperationErrorCode fallback) => exception switch
    {
        ProjectOperationException project => new ProjectOperationError(project.Code, project.Message),
        ConversationHostProjectException host => new ProjectOperationError(fallback, host.Error.Message),
        _ => new ProjectOperationError(fallback, exception.Message),
    };
}
