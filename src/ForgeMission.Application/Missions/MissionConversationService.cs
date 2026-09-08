using ForgeMission.Application.Transport;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Application;

/// <summary>Session-scoped typed access to Host-owned Mission Conversation facts. Project files
/// remain behind MissionVersionService; every individual conversation action first proves the
/// Host snapshot belongs to the requesting session's Project.</summary>
internal sealed class MissionConversationService : IMissionConversationService
{
    private readonly MissionVersionService versions;
    private readonly ApplicationSessionService? sessions;
    private readonly IHttpClientFactory clients;

    internal MissionConversationService(MissionVersionService versions, ApplicationSessionService sessions, IHttpClientFactory clients)
    {
        this.versions = versions;
        this.sessions = sessions;
        this.clients = clients;
    }

    // Retained only for the 45.2 internal reconciliation seam. It has no public transport route;
    // 45.3's public actions always take the session-scoped constructor above.
    internal MissionConversationService(MissionVersionService versions, IHttpClientFactory clients)
    {
        this.versions = versions;
        this.clients = clients;
    }
    public Task<ListApprovedMissionVersionsResponse> ListApprovedAsync(ListApprovedMissionVersionsRequest request, CancellationToken ct) =>
        InSessionAsync(request.SessionId, ct, session => Task.FromResult(new ListApprovedMissionVersionsResponse(
            versions.ListApproved(session.ProjectHome, ct).Select(ToPicker).ToArray(), null)));

    public async Task<ForgeMission.Application.Transport.ListMissionConversationsResponse> ListAsync(ForgeMission.Application.Transport.ListMissionConversationsRequest request, CancellationToken ct) =>
        await InSessionAsync(request.SessionId, ct, async session =>
        {
            var projectId = versions.ReadProjectId(session.ProjectHome, ct);
            var projection = await Host().ListMissionConversationsAsync(projectId, ct);
            return new ForgeMission.Application.Transport.ListMissionConversationsResponse(projection.Conversations.Select(ToView).ToArray(), null);
        });

    public async Task<ForgeMission.Application.Transport.CreateMissionConversationResponse> CreateAsync(ForgeMission.Application.Transport.CreateMissionConversationRequest request, CancellationToken ct) =>
        await InSessionAsync(request.SessionId, ct, async session =>
        {
            var provenance = await versions.ResolveActiveApprovedLaunchAsync(session.ProjectHome, request.MissionId, ct);
            var created = await Host().CreateMissionConversationAsync(new Conversations.Contracts.CreateMissionConversationRequest(
                provenance.ProjectId, request.CommandId, provenance.Launch), ct);
            var view = new MissionConversationView(created.ConversationId, created.Launch.MissionVersionId,
                created.Launch.VersionNumber, created.Launch.Profile.ToString(), ConversationRunStatus.Queued,
                created.AcceptedSequence, DateTimeOffset.UtcNow);
            var version = versions.FindApproved(session.ProjectHome, request.MissionId, created.Launch.MissionVersionId, ct);
            return new ForgeMission.Application.Transport.CreateMissionConversationResponse(view, ToPicker(version), null);
        });

    public async Task<GetMissionConversationResponse> GetAsync(GetMissionConversationRequest request, CancellationToken ct) =>
        await InSessionAsync(request.SessionId, ct, async session =>
        {
            var snapshot = await RequireOwnedAsync(session, request.ConversationId, ct);
            var detail = await Host().ReadMissionConversationDetailAsync(request.ConversationId, ct);
            return new GetMissionConversationResponse(new ForgeMission.Application.Transport.MissionConversationDetail(ToView(snapshot), detail.Detail.Turns.Select(ToTurn).ToArray(), detail.Detail.Events), null);
        });

    public async Task<MissionTurnCommandResponse> SubmitAsync(SubmitMissionConversationTurnRequest request, CancellationToken ct) =>
        await InSessionAsync(request.SessionId, ct, async session =>
        {
            await RequireOwnedAsync(session, request.ConversationId, ct);
            var response = await Host().SubmitMissionTurnAsync(new SubmitMissionTurnRequest(request.ConversationId, request.CommandId, request.Text), ct);
            return new MissionTurnCommandResponse(response.TurnId, response.TurnAttemptId, response.AcceptedSequence, response.Status, null);
        });

    public async Task<MissionTurnCommandResponse> RetryAsync(RetryMissionConversationTurnRequest request, CancellationToken ct) =>
        await InSessionAsync(request.SessionId, ct, async session =>
        {
            await RequireOwnedAsync(session, request.ConversationId, ct);
            var response = await Host().RetryMissionTurnAsync(new RetryMissionTurnRequest(request.ConversationId, request.TurnId, request.CommandId), ct);
            return new MissionTurnCommandResponse(response.TurnId, response.TurnAttemptId, response.AcceptedSequence, response.Status, null);
        });

    public async Task<MissionTurnCommandResponse> CancelAsync(CancelMissionConversationTurnRequest request, CancellationToken ct) =>
        await InSessionAsync(request.SessionId, ct, async session =>
        {
            await RequireOwnedAsync(session, request.ConversationId, ct);
            var response = await Host().CancelMissionTurnAsync(new CancelMissionTurnRequest(request.ConversationId, request.TurnId, request.TurnAttemptId, request.CommandId), ct);
            return new MissionTurnCommandResponse(response.TurnId, response.TurnAttemptId, response.AcceptedSequence, response.Status, null);
        });

    public async Task<GetMissionTurnTraceResponse> GetTraceAsync(GetMissionTurnTraceRequest request, CancellationToken ct) =>
        await InSessionAsync(request.SessionId, ct, async session =>
        {
            var snapshot = await RequireOwnedAsync(session, request.ConversationId, ct);
            var detail = await Host().ReadMissionConversationDetailAsync(request.ConversationId, ct);
            if (!detail.Detail.Turns.Any(turn => turn.TurnId == request.TurnId && turn.TurnAttemptId == request.TurnAttemptId))
                throw new ProjectOperationException(ProjectOperationErrorCode.MissionRunNotFound, "That turn is not in this conversation.");
            var trace = detail.Detail.Events.Where(item => item.RunId == request.TurnAttemptId || item.Kind == ConversationEventKind.UserMessage).ToArray();
            return new GetMissionTurnTraceResponse(new MissionTurnTrace(request.ConversationId, request.TurnId, request.TurnAttemptId, trace), null);
        });

    private async Task<T> InSessionAsync<T>(string sessionId, CancellationToken ct, Func<ApplicationSession, Task<T>> operation) where T : class
    {
        if (sessions is null || !sessions.TryGet(sessionId, out var session) || session is null) return Error<T>("The application session is stale or foreign.");
        try { return await operation(session); }
        catch (ProjectOperationException exception) { return Error<T>(exception.Message, exception.Code); }
        catch (ConversationHostProjectException exception) { return Error<T>(exception.Error.Message); }
        catch (ConversationHostProtocolException exception) { return Error<T>(exception.Message); }
    }

    private async Task<ConversationSnapshot> RequireOwnedAsync(ApplicationSession session, Guid conversationId, CancellationToken ct)
    {
        var snapshot = (await Host().ReadConversationAsync(conversationId, ct)).Snapshot;
        var projectId = versions.ReadProjectId(session.ProjectHome, ct);
        if (snapshot.Purpose != ConversationPurpose.MissionConversation || snapshot.ProjectId != projectId)
            throw new ProjectOperationException(ProjectOperationErrorCode.MissionRunNotFound, "That mission conversation is not in this Project.");
        return snapshot;
    }

    private ConversationHostClient Host() => new(clients.CreateClient("conversation-host"));
    private static MissionVersionPickerItem ToPicker(ApprovedMissionVersion value) => new(value.MissionId, value.Name,
        value.Launch.MissionVersionId, value.Launch.VersionNumber, value.Launch.DefinitionHash, value.Launch.Profile);
    private static MissionConversationView ToView(MissionConversationSummary value) => new(value.ConversationId,
        value.Launch.MissionVersionId, value.Launch.VersionNumber, value.Launch.Profile.ToString(), value.Status, value.LastSequence, value.UpdatedAtUtc);
    private static MissionConversationView ToView(ConversationSnapshot value) => new(value.ConversationId,
        value.PinnedLaunch!.MissionVersionId, value.PinnedLaunch.VersionNumber, value.PinnedLaunch.Profile.ToString(), value.Status, value.LastSequence, value.UpdatedAtUtc);
    private static MissionTurnView ToTurn(ForgeMission.Conversations.Contracts.MissionConversationTurn value) => new(
        value.TurnId, value.TurnAttemptId, value.Input, value.Status, value.AcceptedSequence, value.LastSequence);

    private static T Error<T>(string message, ProjectOperationErrorCode code = ProjectOperationErrorCode.HistoryUnavailable) where T : class
    {
        var error = new ProjectOperationError(code, message);
        object response = typeof(T) == typeof(ListApprovedMissionVersionsResponse) ? new ListApprovedMissionVersionsResponse(null, error) :
            typeof(T) == typeof(ForgeMission.Application.Transport.ListMissionConversationsResponse) ? new ForgeMission.Application.Transport.ListMissionConversationsResponse(null, error) :
            typeof(T) == typeof(ForgeMission.Application.Transport.CreateMissionConversationResponse) ? new ForgeMission.Application.Transport.CreateMissionConversationResponse(null, null, error) :
            typeof(T) == typeof(GetMissionConversationResponse) ? new GetMissionConversationResponse(null, error) :
            typeof(T) == typeof(MissionTurnCommandResponse) ? new MissionTurnCommandResponse(null, null, null, null, error) :
            typeof(T) == typeof(GetMissionTurnTraceResponse) ? new GetMissionTurnTraceResponse(null, error) :
            throw new InvalidOperationException($"Unsupported Mission Conversation response: {typeof(T).Name}.");
        return (T)response;
    }

    // Phase 45.2 internal coordinator entry points, intentionally unavailable through the
    // Application Transport. Evaluation remains Project-owned and no UI action reaches it here.
    internal async Task<ForgeMission.Conversations.Contracts.CreateMissionConversationResponse> CreateAsync(
        string home, Guid missionId, Guid commandId, CancellationToken ct)
    {
        var provenance = await versions.ResolveActiveApprovedLaunchAsync(home, missionId, ct);
        return await Host().CreateMissionConversationAsync(new ForgeMission.Conversations.Contracts.CreateMissionConversationRequest(
            provenance.ProjectId, commandId, provenance.Launch), ct);
    }

    internal async Task<EvaluationResult> StartEvaluationAsync(string home, Guid missionId, Guid missionVersionId, Guid caseId, CancellationToken ct)
    {
        var pending = await versions.CreatePendingEvaluationAsync(home, missionId, missionVersionId, caseId, ct);
        var launch = new DurableMissionLaunch(pending.Version.MissionVersionId, pending.Version.VersionNumber,
            pending.Version.DefinitionHash, pending.Version.DefinitionText, pending.Version.CapabilityProfile, pending.Version.Package);
        StartEvaluationResponse response;
        try
        {
            response = await Host().StartEvaluationAsync(new StartEvaluationRequest(pending.ProjectId, pending.MissionId,
                pending.Version.MissionVersionId, pending.EvaluationCase.EvaluationCaseId, pending.Result.EvaluationResultId,
                pending.Version.CandidateRevision, pending.Version.DefinitionHash, launch, pending.EvaluationCase.Input), ct);
        }
        catch (ConversationHostProjectException exception) when (exception.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.Conflict)
        {
            return await versions.ReconcilePendingCompletionAsync(home, missionId, missionVersionId, caseId,
                pending.Result.EvaluationResultId, pending.Version.CandidateRevision, pending.Version.DefinitionHash,
                EvaluationOutcome.Failed, exception.Error.Message, null, ct);
        }
        if (!response.Projection.IsTerminal) return pending.Result;
        var outcome = response.Projection.ObservedOutcome == EvaluationOutcomeWire.Succeeded ? EvaluationOutcome.Succeeded : EvaluationOutcome.Failed;
        var trace = response.Projection.TraceOrigin is { } origin ? new EvaluationTraceOrigin(origin.ConversationId, origin.TurnId, origin.TurnAttemptId) : null;
        return await versions.ReconcilePendingCompletionAsync(home, pending.MissionId, pending.Version.MissionVersionId,
            pending.EvaluationCase.EvaluationCaseId, pending.Result.EvaluationResultId, pending.Version.CandidateRevision,
            pending.Version.DefinitionHash, outcome, response.Projection.ObservedOutputSummary ?? response.Projection.Reason ?? "", trace, ct);
    }
}
