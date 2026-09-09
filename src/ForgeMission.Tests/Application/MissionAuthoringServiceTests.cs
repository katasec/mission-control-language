using System.Net;
using System.Text;
using System.Text.Json;
using ForgeMission.Application;
using ForgeMission.Application.Transport;
using ForgeMission.ClientRuntime;
using ForgeMission.Conversations.Contracts;
using ProjectEvaluationOutcome = ForgeMission.Application.EvaluationOutcome;

namespace ForgeMission.Tests.Application;

/// <summary>
/// Phase 45.4 minimum. The thing worth guarding is that authoring never decides anything: Projects
/// owns the lifecycle and the publish guard, and this service only sequences and projects it. So
/// these assert what it refuses as much as what it does.
/// </summary>
public sealed class MissionAuthoringServiceTests : IDisposable
{
    private readonly string _profile = Directory.CreateTempSubdirectory("forge-authoring-").FullName;

    public void Dispose() => Directory.Delete(_profile, recursive: true);

    [Fact]
    public async Task AuthoringAMission_ScaffoldsThePackageAssetsItNeeds_ThenPromotes()
    {
        var fixture = NewFixture();
        var created = await fixture.Service.CreateDraftAsync(
            new CreateMissionDraftRequest(fixture.SessionId, "Janus", string.Empty, MissionHandsProfile.NoHands), Ct);

        Assert.Null(created.Error);
        var draft = created.Authoring!.Open!;
        Assert.Equal(MissionEditableKind.Draft, draft.Editable);
        Assert.Contains("mission Janus(task)", draft.DefinitionText, StringComparison.Ordinal);
        // A draft cannot be evaluated, and the surface is told why rather than left to guess.
        Assert.False(draft.CanPublish);
        Assert.NotNull(draft.PublishBlockedReason);

        var promoted = await fixture.Service.PromoteCandidateAsync(
            new PromoteMissionCandidateRequest(fixture.SessionId, draft.MissionId, draft.DraftId!.Value, draft.Revision), Ct);

        Assert.Null(promoted.Error);
        var candidate = promoted.Authoring!.Open!;
        Assert.Equal(MissionEditableKind.Candidate, candidate.Editable);
        Assert.Equal(1, candidate.VersionNumber);
        Assert.Equal("Add at least one evaluation case, then evaluate this candidate.", candidate.PublishBlockedReason);
    }

    [Fact]
    public async Task PublishIsBlocked_UntilEveryCaseHasPassedAgainstThisCandidate()
    {
        var fixture = NewFixture();
        var candidate = await CandidateAsync(fixture);

        var added = await fixture.Service.AddCaseAsync(new AddEvaluationCaseRequest(
            fixture.SessionId, candidate.MissionId, candidate.MissionVersionId!.Value,
            new EvaluationCaseInput("plan the cutover", null, null, EvaluationOutcomeView.Succeeded, ["approved"])), Ct);
        var withCase = added.Authoring!.Open!;
        Assert.Single(withCase.Cases);
        Assert.Equal(EvaluationResultStateView.None, withCase.Cases[0].ResultState);
        Assert.False(withCase.CanPublish);
        Assert.Contains("evaluated", withCase.PublishBlockedReason!, StringComparison.OrdinalIgnoreCase);

        // Application refuses regardless of what a surface believes about the button.
        var refused = await fixture.Service.PublishAsync(
            new PublishMissionVersionRequest(fixture.SessionId, candidate.MissionId, candidate.MissionVersionId.Value), Ct);
        Assert.NotNull(refused.Error);
        Assert.Equal(ProjectOperationErrorCode.PublishConflict, refused.Error!.Code);
    }

    [Fact]
    public async Task AFailingCaseKeepsPublishBlocked_ThenCorrectingTheCaseAndRerunningUnblocksIt()
    {
        var fixture = NewFixture(observed: "declined: the dependency cannot be satisfied");
        var candidate = await CandidateAsync(fixture);
        var missionId = candidate.MissionId;
        var versionId = candidate.MissionVersionId!.Value;

        // The answer will not contain "approved", so this case must fail.
        var added = await fixture.Service.AddCaseAsync(new AddEvaluationCaseRequest(
            fixture.SessionId, missionId, versionId,
            new EvaluationCaseInput("plan the cutover", null, null, EvaluationOutcomeView.Succeeded, ["approved"])), Ct);
        var caseId = added.Authoring!.Open!.Cases[0].EvaluationCaseId;

        var ran = await fixture.Service.RunCaseAsync(new RunEvaluationCaseRequest(fixture.SessionId, missionId, versionId, caseId), Ct);
        var failed = ran.Authoring!.Open!.Cases[0];
        Assert.Equal(EvaluationResultStateView.Failed, failed.ResultState);
        Assert.Equal("declined: the dependency cannot be satisfied", failed.ObservedSummary);
        Assert.NotNull(failed.TraceReference);
        Assert.False(ran.Authoring.Open.CanPublish);
        Assert.Contains("did not match", ran.Authoring.Open.PublishBlockedReason!, StringComparison.OrdinalIgnoreCase);

        // Correcting the case clears its stale verdict rather than leaving a result for a question
        // nobody asked any more.
        var edited = await fixture.Service.UpdateCaseAsync(new UpdateEvaluationCaseRequest(
            fixture.SessionId, missionId, versionId, caseId,
            new EvaluationCaseInput("plan the cutover", null, null, EvaluationOutcomeView.Succeeded, ["declined"])), Ct);
        Assert.Equal(EvaluationResultStateView.None, edited.Authoring!.Open!.Cases[0].ResultState);
        Assert.False(edited.Authoring.Open.CanPublish);

        var reran = await fixture.Service.RunCaseAsync(new RunEvaluationCaseRequest(fixture.SessionId, missionId, versionId, caseId), Ct);
        Assert.Equal(EvaluationResultStateView.Passed, reran.Authoring!.Open!.Cases[0].ResultState);
        Assert.True(reran.Authoring.Open.CanPublish);
        Assert.Null(reran.Authoring.Open.PublishBlockedReason);

        var published = await fixture.Service.PublishAsync(new PublishMissionVersionRequest(fixture.SessionId, missionId, versionId), Ct);
        Assert.Null(published.Error);
        var summary = Assert.Single(published.Authoring!.Missions);
        Assert.Equal(MissionVersionStateView.Approved, summary.LatestState);
        Assert.Equal(1, summary.LatestVersionNumber);
    }

    [Fact]
    public async Task AnUnreachableEvaluationHostIsATypedFailure_NotAVerdict()
    {
        var fixture = NewFixture(hostFails: true);
        var candidate = await CandidateAsync(fixture);
        var added = await fixture.Service.AddCaseAsync(new AddEvaluationCaseRequest(
            fixture.SessionId, candidate.MissionId, candidate.MissionVersionId!.Value,
            new EvaluationCaseInput("plan the cutover", null, null, EvaluationOutcomeView.Succeeded, [])), Ct);
        var caseId = added.Authoring!.Open!.Cases[0].EvaluationCaseId;

        var ran = await fixture.Service.RunCaseAsync(new RunEvaluationCaseRequest(
            fixture.SessionId, candidate.MissionId, candidate.MissionVersionId.Value, caseId), Ct);

        Assert.NotNull(ran.Error);
        // The document still shows current truth beside the refusal, and nothing became a pass.
        Assert.NotNull(ran.Authoring);
        Assert.False(ran.Authoring!.Open!.CanPublish);
        Assert.DoesNotContain(ran.Authoring.Open.Cases, item => item.ResultState is EvaluationResultStateView.Passed);
    }

    [Fact]
    public async Task AForeignSessionReachesNothing()
    {
        var fixture = NewFixture();
        var foreign = Guid.NewGuid().ToString("N");

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.GetAsync(new GetMissionAuthoringRequest(foreign, null), Ct));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.CreateDraftAsync(
            new CreateMissionDraftRequest(foreign, "Janus", string.Empty, MissionHandsProfile.NoHands), Ct));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.PublishAsync(
            new PublishMissionVersionRequest(foreign, Guid.NewGuid(), Guid.NewGuid()), Ct));
    }

    // ── fixture ─────────────────────────────────────────────────────────────────────────────

    private static CancellationToken Ct => CancellationToken.None;

    private async Task<MissionAuthoringDocument> CandidateAsync(Fixture fixture)
    {
        var created = await fixture.Service.CreateDraftAsync(
            new CreateMissionDraftRequest(fixture.SessionId, "Janus", string.Empty, MissionHandsProfile.NoHands), Ct);
        var draft = created.Authoring!.Open!;
        var promoted = await fixture.Service.PromoteCandidateAsync(
            new PromoteMissionCandidateRequest(fixture.SessionId, draft.MissionId, draft.DraftId!.Value, draft.Revision), Ct);
        return promoted.Authoring!.Open!;
    }

    private Fixture NewFixture(string observed = "approved: the sequence is sound", bool hostFails = false)
    {
        var projects = new ProjectService(Path.Combine(_profile, "Forge", "Projects"));
        var project = projects.Create("Author and publish a mission", null, null);
        var sessions = new ApplicationSessionService(ForgeMission.Core.Tools.CapabilityAuthorizationPolicy.Default, _ => { }, Ct);
        var session = sessions.CreateForProject(project.Home);
        var versions = new MissionVersionService(projects);
        // A controlled Conversation Host: the evaluation returns terminally on admission, which is
        // the shape the real Host uses for an already-finished run.
        var conversations = new MissionConversationService(versions, new Factory(new Handler(async request =>
        {
            if (hostFails)
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent(JsonSerializer.Serialize(
                        new ConversationApiError("unavailable", "the evaluation runtime is unavailable"),
                        ConversationContractsJsonContext.Default.ConversationApiError), Encoding.UTF8, "application/json"),
                };
            var body = await request.Content!.ReadAsStringAsync();
            var start = JsonSerializer.Deserialize(body, ConversationContractsJsonContext.Default.StartEvaluationRequest)!;
            var trace = new EvaluationTraceOriginWire(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            var projection = new EvaluationProjection(start.EvaluationResultId, trace.ConversationId, trace.TurnId,
                trace.TurnAttemptId, true, EvaluationOutcomeWire.Succeeded, observed, trace, null);
            return new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent(JsonSerializer.Serialize(new StartEvaluationResponse(projection),
                    ConversationContractsJsonContext.Default.StartEvaluationResponse), Encoding.UTF8, "application/json"),
            };
        })), sessions);
        return new Fixture(session.Id, new MissionAuthoringService(projects, versions, conversations, sessions));
    }

    private sealed record Fixture(string SessionId, IMissionAuthoringService Service);

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = new Uri("http://conversation-host/") };
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request);
    }
}
