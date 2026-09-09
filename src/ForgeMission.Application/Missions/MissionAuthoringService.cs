using ForgeMission.Application.Transport;
using ForgeMission.Conversations.Contracts;
using ProjectEvaluationCase = ForgeMission.Application.EvaluationCase;
using ProjectEvaluationOutcome = ForgeMission.Application.EvaluationOutcome;

namespace ForgeMission.Application;

/// <summary>
/// The Missions-side coordinator for authoring (45.4 minimum). It owns no rule: Projects decides
/// the version lifecycle and what may publish, the durable owners decide evaluation, and this
/// class only sequences them for one surface and projects their answers.
/// <para>
/// Evaluation has no event channel, so the read action reconciles every pending result before it
/// answers. That keeps "what does this candidate currently prove" in one place rather than asking
/// a surface to poll a second endpoint and stitch the two together.
/// </para>
/// </summary>
internal sealed class MissionAuthoringService(
    ProjectService projects,
    MissionVersionService versions,
    MissionConversationService conversations,
    ApplicationSessionService? sessions = null) : IMissionAuthoringService
{
    public async Task<GetMissionAuthoringResponse> GetAsync(GetMissionAuthoringRequest request, CancellationToken ct)
    {
        var home = RequiredHome(request.SessionId);
        try
        {
            if (request.MissionId is { } missionId)
                await ReconcilePendingAsync(home, missionId, ct);
            return new GetMissionAuthoringResponse(Project(home, request.MissionId), null);
        }
        catch (Exception exception) when (Expected(exception))
        {
            return new GetMissionAuthoringResponse(null, ToError(exception));
        }
    }

    public async Task<MissionAuthoringMutationResponse> CreateDraftAsync(CreateMissionDraftRequest request, CancellationToken ct)
    {
        var home = RequiredHome(request.SessionId);
        try
        {
            // The scaffold is what makes promotion possible at all, so it happens before the first
            // draft rather than at some later step the operator would hit as a dead end.
            await projects.EnsureMissionAssetsAsync(home, ct);
            var definition = await versions.CreateDraftAsync(home, request.Name,
                Blank(request.DefinitionText) ? ProjectService.StarterMissionDefinition : request.DefinitionText,
                request.Profile, ct);
            return Mutated(home, definition.MissionId);
        }
        catch (Exception exception) when (Expected(exception))
        {
            return new MissionAuthoringMutationResponse(null, ToError(exception));
        }
    }

    public Task<MissionAuthoringMutationResponse> SaveDraftAsync(SaveMissionDraftRequest request, CancellationToken ct) =>
        MutateAsync(request.SessionId, request.MissionId, (home, ct) =>
            versions.SaveDraftAsync(home, request.MissionId, request.DraftId, request.Revision,
                request.DefinitionText, request.Profile, ct), ct);

    public Task<MissionAuthoringMutationResponse> PromoteCandidateAsync(PromoteMissionCandidateRequest request, CancellationToken ct) =>
        MutateAsync(request.SessionId, request.MissionId, (home, ct) =>
            versions.PromoteCandidateAsync(home, request.MissionId, request.DraftId, request.Revision, ct), ct);

    public Task<MissionAuthoringMutationResponse> AddCaseAsync(AddEvaluationCaseRequest request, CancellationToken ct) =>
        MutateAsync(request.SessionId, request.MissionId, (home, ct) =>
            versions.SaveCaseAsync(home, request.MissionId, request.MissionVersionId, ToCase(Guid.NewGuid(), request.Case, 1), ct), ct);

    public Task<MissionAuthoringMutationResponse> UpdateCaseAsync(UpdateEvaluationCaseRequest request, CancellationToken ct) =>
        MutateAsync(request.SessionId, request.MissionId, (home, ct) =>
            versions.UpdateCaseAsync(home, request.MissionId, request.MissionVersionId, ToCase(request.EvaluationCaseId, request.Case, 1), ct), ct);

    public Task<MissionAuthoringMutationResponse> RunCaseAsync(RunEvaluationCaseRequest request, CancellationToken ct) =>
        MutateAsync(request.SessionId, request.MissionId, (home, ct) =>
            conversations.StartEvaluationAsync(home, request.MissionId, request.MissionVersionId, request.EvaluationCaseId, ct), ct);

    public Task<MissionAuthoringMutationResponse> PublishAsync(PublishMissionVersionRequest request, CancellationToken ct) =>
        MutateAsync(request.SessionId, request.MissionId, (home, ct) =>
            versions.PublishAsync(home, request.MissionId, request.MissionVersionId, ct), ct);

    // ── projection ──────────────────────────────────────────────────────────────────────────

    private MissionAuthoringProjection Project(string home, Guid? missionId)
    {
        var manifest = projects.ReadForHome(home).Manifest;
        var definitions = manifest.MissionDefinitions ?? [];
        var summaries = definitions.Select(Summary).ToArray();
        var open = missionId is { } id
            ? Document(definitions.SingleOrDefault(item => item.MissionId == id)
                ?? throw new ProjectOperationException(ProjectOperationErrorCode.UnknownMission, "That Project mission no longer exists."))
            : null;
        return new MissionAuthoringProjection(summaries, open);
    }

    // Mapped, never cast. EvaluationResultStateView adds a "not run yet" value the durable enum
    // has no counterpart for, so the two no longer share ordinals and a cast would silently
    // report a failure as a pass.
    private static EvaluationResultStateView ResultState(EvaluationResult? result) => result?.State switch
    {
        EvaluationResultState.Pending => EvaluationResultStateView.Pending,
        EvaluationResultState.Passed => EvaluationResultStateView.Passed,
        EvaluationResultState.Failed => EvaluationResultStateView.Failed,
        _ => EvaluationResultStateView.None,
    };

    private static EvaluationOutcomeView Outcome(ProjectEvaluationOutcome outcome) =>
        outcome == ProjectEvaluationOutcome.Failed ? EvaluationOutcomeView.Failed : EvaluationOutcomeView.Succeeded;

    private static MissionVersionStateView VersionState(MissionVersionState state) => state switch
    {
        MissionVersionState.Candidate => MissionVersionStateView.Candidate,
        MissionVersionState.Evaluated => MissionVersionStateView.Evaluated,
        MissionVersionState.Approved => MissionVersionStateView.Approved,
        _ => MissionVersionStateView.Superseded,
    };

    private static MissionDefinitionSummary Summary(ProjectMissionDefinition definition)
    {
        var latest = (definition.Versions ?? []).OrderByDescending(version => version.VersionNumber).FirstOrDefault();
        return new MissionDefinitionSummary(definition.MissionId, definition.Name,
            latest?.VersionNumber, latest is null ? null : VersionState(latest.State),
            definition.Draft is not null);
    }

    /// <summary>What is editable right now: a draft if one exists, otherwise the newest candidate.
    /// An Approved or Superseded version is history and is not opened for editing here.</summary>
    private static MissionAuthoringDocument Document(ProjectMissionDefinition definition)
    {
        var versions = definition.Versions ?? [];
        var candidate = versions
            .Where(version => version.State is MissionVersionState.Candidate or MissionVersionState.Evaluated)
            .OrderByDescending(version => version.VersionNumber)
            .FirstOrDefault();

        if (definition.Draft is { } draft)
            return new MissionAuthoringDocument(definition.MissionId, definition.Name,
                MissionEditableKind.Draft, draft.CapabilityProfile, draft.DraftId, null,
                draft.Revision, null, draft.DefinitionText, [], false,
                "Promote this draft to a candidate before evaluating it.");

        if (candidate is null)
            return new MissionAuthoringDocument(definition.MissionId, definition.Name,
                MissionEditableKind.None, MissionHandsProfile.NoHands, null, null, 0, null, string.Empty, [],
                false, "This mission has no editable draft or candidate.");

        var cases = (candidate.EvaluationCases ?? []).Select(item => CaseView(candidate, item)).ToArray();
        var (canPublish, blocked) = PublishDisposition(candidate, cases);
        return new MissionAuthoringDocument(definition.MissionId, definition.Name,
            MissionEditableKind.Candidate, candidate.CapabilityProfile, null, candidate.MissionVersionId,
            candidate.CandidateRevision, candidate.VersionNumber, candidate.DefinitionText, cases,
            canPublish, blocked);
    }

    private static EvaluationCaseView CaseView(MissionVersion version, ProjectEvaluationCase item)
    {
        // Only a result recorded against this exact revision and hash describes this candidate.
        var result = (version.EvaluationResults ?? []).SingleOrDefault(entry =>
            entry.EvaluationCaseId == item.EvaluationCaseId &&
            entry.CandidateRevision == version.CandidateRevision &&
            string.Equals(entry.DefinitionHash, version.DefinitionHash, StringComparison.Ordinal));
        return new EvaluationCaseView(item.EvaluationCaseId, item.Input, item.ExpectedSuccess, item.ExpectedFailure,
            Outcome(item.ExpectedOutcome), item.RequiredOutputFragments ?? [],
            ResultState(result),
            result?.ObservedOutputSummary,
            result?.TraceOrigin is { } origin ? origin.ConversationId.ToString("N")[..12] : null);
    }

    /// <summary>Projects the lifecycle Projects already enforces; it never decides publish itself.
    /// PublishAsync remains the guard, so a surface that ignored this still cannot publish.</summary>
    private static (bool CanPublish, string? Blocked) PublishDisposition(MissionVersion version, EvaluationCaseView[] cases)
    {
        if (cases.Length == 0)
            return (false, "Add at least one evaluation case, then evaluate this candidate.");
        if (cases.Any(item => item.ResultState is EvaluationResultStateView.None))
            return (false, "Every case must be evaluated against this candidate before it can be published.");
        if (cases.Any(item => item.ResultState is EvaluationResultStateView.Pending))
            return (false, "An evaluation is still running.");
        var failed = cases.Where(item => item.ResultState is EvaluationResultStateView.Failed).ToArray();
        if (failed.Length > 0)
            return (false, $"{failed.Length} of {cases.Length} cases did not match. Edit the case or the definition, then re-evaluate.");
        return version.State == MissionVersionState.Evaluated
            ? (true, null)
            : (false, "This candidate has not reached its evaluated state.");
    }

    // ── plumbing ────────────────────────────────────────────────────────────────────────────

    private async Task<MissionAuthoringMutationResponse> MutateAsync(
        string sessionId, Guid missionId, Func<string, CancellationToken, Task> mutate, CancellationToken ct)
    {
        var home = RequiredHome(sessionId);
        try
        {
            await mutate(home, ct);
            return Mutated(home, missionId);
        }
        catch (Exception exception) when (Expected(exception))
        {
            // The refusal is the answer, and the document still shows current truth beside it.
            return new MissionAuthoringMutationResponse(SafeProjection(home, missionId), ToError(exception));
        }
    }

    private MissionAuthoringMutationResponse Mutated(string home, Guid missionId) =>
        new(Project(home, missionId), null);

    private MissionAuthoringProjection? SafeProjection(string home, Guid missionId)
    {
        try { return Project(home, missionId); }
        catch (ProjectOperationException) { return null; }
    }

    /// <summary>Brings every pending result to its terminal state before the surface reads it.
    /// A result that is still genuinely running stays Pending; nothing is fabricated.</summary>
    private async Task ReconcilePendingAsync(string home, Guid missionId, CancellationToken ct)
    {
        ProjectMissionDefinition? definition;
        try { definition = (projects.ReadForHome(home).Manifest.MissionDefinitions ?? []).SingleOrDefault(item => item.MissionId == missionId); }
        catch (ProjectOperationException) { return; }
        if (definition is null) return;

        foreach (var version in definition.Versions ?? [])
        {
            foreach (var pending in (version.EvaluationResults ?? []).Where(result => result.State == EvaluationResultState.Pending))
            {
                try
                {
                    await conversations.ReconcileAsync(home, missionId, version.MissionVersionId,
                        pending.EvaluationCaseId, pending.EvaluationResultId, ct);
                }
                catch (Exception exception) when (Expected(exception))
                {
                    // Still running, or the Host cannot be reached. Either way the result stays
                    // Pending and the surface says so; a read must not turn that into a verdict.
                }
            }
        }
    }

    private static ProjectEvaluationCase ToCase(Guid id, EvaluationCaseInput input, int revision) =>
        new(id, input.Input, input.ExpectedSuccess ?? string.Empty, input.ExpectedFailure ?? string.Empty,
            input.ExpectedOutcome == EvaluationOutcomeView.Failed ? ProjectEvaluationOutcome.Failed : ProjectEvaluationOutcome.Succeeded,
            (input.RequiredFragments ?? []).Where(fragment => !string.IsNullOrWhiteSpace(fragment)).ToArray(),
            [], revision);

    private string RequiredHome(string sessionId) =>
        sessions is not null && sessions.TryGet(sessionId, out var session) && session is not null
            ? session.ProjectHome
            : throw new KeyNotFoundException();

    private static bool Expected(Exception exception) =>
        exception is ProjectOperationException or ConversationHostProjectException or HttpRequestException;

    private static ProjectOperationError ToError(Exception exception) => exception switch
    {
        ProjectOperationException project => new ProjectOperationError(project.Code, project.Message),
        ConversationHostProjectException host => new ProjectOperationError(ProjectOperationErrorCode.EvaluationUnavailable, host.Error.Message),
        _ => new ProjectOperationError(ProjectOperationErrorCode.EvaluationUnavailable, exception.Message),
    };

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);
}
