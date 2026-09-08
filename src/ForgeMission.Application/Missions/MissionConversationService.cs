using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Application;

/// <summary>Coordinates Project-owned immutable values with the durable Host. It never writes a
/// manifest directly, sequences durable facts, or creates a local capability attachment.</summary>
internal sealed class MissionConversationService(MissionVersionService versions, IHttpClientFactory clients)
{
    public async Task<CreateMissionConversationResponse> CreateAsync(string home, Guid missionId, Guid commandId, CancellationToken ct)
    {
        var provenance = await versions.ResolveActiveApprovedLaunchAsync(home, missionId, ct);
        return await Host().CreateMissionConversationAsync(new CreateMissionConversationRequest(
            provenance.ProjectId, commandId, provenance.Launch), ct);
    }

    public async Task<ListMissionConversationsResponse> ListAsync(string home, CancellationToken ct)
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
}
