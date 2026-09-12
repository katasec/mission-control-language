using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ForgeMission.Application;
using ForgeMission.ClientRuntime;
using ForgeMission.Core.Tools;
using ForgeMission.Application.Transport;
using ForgeMission.Conversations.Contracts;
// Contracts and Transport each name this operation's request/response, one per side of it.
using HostCreateMissionConversationRequest = ForgeMission.Conversations.Contracts.CreateMissionConversationRequest;
using HostCreateMissionConversationResponse = ForgeMission.Conversations.Contracts.CreateMissionConversationResponse;
using HostListMissionConversationsResponse = ForgeMission.Conversations.Contracts.ListMissionConversationsResponse;
using SurfaceListMissionConversationsRequest = ForgeMission.Application.Transport.ListMissionConversationsRequest;
using SurfaceCreateMissionConversationRequest = ForgeMission.Application.Transport.CreateMissionConversationRequest;

namespace ForgeMission.Tests.Application;

public sealed class MissionConversationServiceTests : IDisposable
{
    private readonly string profile = Path.Combine(Path.GetTempPath(), "forge-mission-conversations", Guid.NewGuid().ToString("N"));
    private readonly ProjectService projects;
    private readonly MissionVersionService versions;

    public MissionConversationServiceTests()
    {
        projects = new ProjectService(Path.Combine(profile, "Forge", "Projects"));
        versions = new MissionVersionService(projects);
    }

    [Fact]
    public async Task ApprovedVersion_CreatePassesOnlyImmutableLaunchToHost()
    {
        var (project, missionId, approved) = await CreateApprovedAsync();
        HostCreateMissionConversationRequest? captured = null;
        var service = Service(async request =>
        {
            Assert.Equal("/mission-conversations", request.RequestUri!.AbsolutePath);
            var body = await request.Content!.ReadAsStringAsync();
            Assert.DoesNotContain(project.Home, body, StringComparison.Ordinal);
            Assert.DoesNotContain("capabilities", body, StringComparison.OrdinalIgnoreCase);
            captured = JsonSerializer.Deserialize(body, ConversationContractsJsonContext.Default.CreateMissionConversationRequest);
            var response = new HostCreateMissionConversationResponse(Guid.NewGuid(), 0, captured!.Launch);
            return Json(response, ConversationContractsJsonContext.Default.CreateMissionConversationResponse, HttpStatusCode.Created);
        });

        var result = await service.CreateAsync(project.Home, missionId, Guid.NewGuid(), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(project.Manifest.ProjectId, captured!.ProjectId);
        Assert.Equal(approved.MissionVersionId, captured.Launch.MissionVersionId);
        Assert.Equal(approved.Package.PackageHash, captured.Launch.Package!.PackageHash);
        Assert.Equal(captured.Launch.MissionVersionId, result.Launch.MissionVersionId);
        Assert.Equal(captured.Launch.DefinitionHash, result.Launch.DefinitionHash);
        Assert.Equal(captured.Launch.Package!.PackageHash, result.Launch.Package!.PackageHash);
    }

    [Fact]
    public async Task CandidateEvaluation_ReconcilesThePendingIdentityFromTerminalHostProjection()
    {
        var (project, missionId, candidate, evaluationCase) = await CreateCandidateAsync();
        StartEvaluationRequest? captured = null;
        var service = Service(async request =>
        {
            captured = JsonSerializer.Deserialize(await request.Content!.ReadAsStringAsync(), ConversationContractsJsonContext.Default.StartEvaluationRequest);
            var trace = new EvaluationTraceOriginWire(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            var projection = new EvaluationProjection(captured!.EvaluationResultId, trace.ConversationId, trace.TurnId,
                trace.TurnAttemptId, true, EvaluationOutcomeWire.Succeeded, "observed", trace, null);
            return Json(new StartEvaluationResponse(projection), ConversationContractsJsonContext.Default.StartEvaluationResponse, HttpStatusCode.Accepted);
        });

        var result = await service.StartEvaluationAsync(project.Home, missionId, candidate.MissionVersionId,
            evaluationCase.EvaluationCaseId, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(captured!.EvaluationResultId, result.EvaluationResultId);
        Assert.Equal(EvaluationResultState.Passed, result.State);
        Assert.NotNull(result.TraceOrigin);
        var stored = Assert.Single(await versions.ListResultsAsync(project.Home, missionId, candidate.MissionVersionId, CancellationToken.None));
        Assert.Equal(captured.EvaluationResultId, stored.EvaluationResultId);
    }

    [Fact]
    public async Task CandidateEvaluation_DefinitiveHostRejectionFailsTheSamePendingResultWithoutTrace()
    {
        var (project, missionId, candidate, evaluationCase) = await CreateCandidateAsync();
        StartEvaluationRequest? captured = null;
        var service = Service(async request =>
        {
            captured = JsonSerializer.Deserialize(await request.Content!.ReadAsStringAsync(), ConversationContractsJsonContext.Default.StartEvaluationRequest);
            return Json(new ConversationApiError("invalidRequest", "Host refused the evaluation."),
                ConversationContractsJsonContext.Default.ConversationApiError, HttpStatusCode.BadRequest);
        });

        var result = await service.StartEvaluationAsync(project.Home, missionId, candidate.MissionVersionId,
            evaluationCase.EvaluationCaseId, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(captured!.EvaluationResultId, result.EvaluationResultId);
        Assert.Equal(EvaluationResultState.Failed, result.State);
        Assert.Null(result.TraceOrigin);
        Assert.Equal("Host refused the evaluation.", result.ObservedOutputSummary);
    }

    // ── Missions landing surface actions (45.3 task 3A) ────────────────────────────────────

    [Fact]
    public async Task Landing_ListsOnlyApprovedVersions_AndNeverALocalPathOrPackage()
    {
        var (project, missionId, approved) = await CreateApprovedAsync();
        // A second mission stops at candidate: it is a real version, but not a startable one.
        var draft = await versions.CreateDraftAsync(project.Home, "Unpublished", Source, MissionHandsProfile.NoHands, CancellationToken.None);
        await versions.PromoteCandidateAsync(project.Home, draft.MissionId, draft.Draft!.DraftId, draft.Draft.Revision, CancellationToken.None);

        var (service, session) = SurfaceService(project.Home, _ => throw new InvalidOperationException("no Host call expected"));
        var response = await service.ListApprovedVersionsAsync(new ListApprovedMissionVersionsRequest(session.Id), CancellationToken.None);

        var option = Assert.Single(response.Options!);
        Assert.Null(response.Error);
        Assert.Equal(approved.MissionVersionId, option.MissionVersionId);
        Assert.Equal(missionId, option.MissionId);
        Assert.Equal(MissionHandsProfile.NoHands, option.Profile);
        Assert.DoesNotContain(project.Home, JsonSerializer.Serialize(response, ApplicationJsonContext.Default.ListApprovedMissionVersionsResponse), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Landing_NamesAConversationFromItsPinnedVersion_EvenAfterThatVersionIsSuperseded()
    {
        var (project, missionId, approved) = await CreateApprovedAsync();
        var pinned = approved;
        // Publishing again supersedes the pinned version. The conversation still ran on it, so the
        // row must still name it rather than borrow the newly approved version's identity.
        var next = await PublishNextAsync(project.Home, missionId);
        Assert.NotEqual(pinned.MissionVersionId, next.MissionVersionId);

        var summary = new MissionConversationSummary(Guid.NewGuid(), project.Manifest.ProjectId,
            new DurableMissionLaunch(pinned.MissionVersionId, pinned.VersionNumber, pinned.DefinitionHash,
                pinned.DefinitionText, pinned.CapabilityProfile, pinned.Package),
            ConversationRunStatus.Queued, 3, DateTimeOffset.UtcNow);
        var (service, session) = SurfaceService(project.Home, _ => Task.FromResult(Json(
            new HostListMissionConversationsResponse([summary]),
            ConversationContractsJsonContext.Default.ListMissionConversationsResponse, HttpStatusCode.OK)));

        var response = await service.ListAsync(new SurfaceListMissionConversationsRequest(session.Id), CancellationToken.None);

        var row = Assert.Single(response.Conversations!);
        Assert.Equal("Review", row.MissionName);
        Assert.Equal(pinned.VersionNumber, row.VersionNumber);
    }

    [Fact]
    public async Task Landing_ListsAConversationWithoutAName_WhenItsPinnedVersionIsNotHeldLocally()
    {
        var project = CreatePackagedProject();
        var summary = new MissionConversationSummary(Guid.NewGuid(), project.Manifest.ProjectId,
            new DurableMissionLaunch(Guid.NewGuid(), 7, "sha256:whatever", "definition", MissionHandsProfile.NoHands),
            ConversationRunStatus.Queued, 3, DateTimeOffset.UtcNow);
        var (service, session) = SurfaceService(project.Home, _ => Task.FromResult(Json(
            new HostListMissionConversationsResponse([summary]),
            ConversationContractsJsonContext.Default.ListMissionConversationsResponse, HttpStatusCode.OK)));

        var response = await service.ListAsync(new SurfaceListMissionConversationsRequest(session.Id), CancellationToken.None);

        var row = Assert.Single(response.Conversations!);
        Assert.Null(row.MissionName);
        Assert.Equal(7, row.VersionNumber);
        Assert.Equal(summary.ConversationId, row.ConversationId);
    }

    [Fact]
    public async Task Landing_HostFailureIsATypedAvailabilityResult_NotAnException()
    {
        var project = CreatePackagedProject();
        var (service, session) = SurfaceService(project.Home, _ => Task.FromResult(Json(
            new ConversationApiError("unavailable", "the directory is unavailable"),
            ConversationContractsJsonContext.Default.ConversationApiError, HttpStatusCode.ServiceUnavailable)));

        var response = await service.ListAsync(new SurfaceListMissionConversationsRequest(session.Id), CancellationToken.None);

        Assert.Null(response.Conversations);
        Assert.Equal(ProjectOperationErrorCode.HistoryUnavailable, response.Error!.Code);
    }

    [Fact]
    public async Task Create_RefusesWhenTheApprovedVersionMovedSinceItWasDisplayed_AndCallsNoHost()
    {
        var (project, missionId, displayed) = await CreateApprovedAsync();
        var republished = await PublishNextAsync(project.Home, missionId);
        Assert.NotEqual(displayed.MissionVersionId, republished.MissionVersionId);

        var calls = 0;
        var (service, session) = SurfaceService(project.Home, _ => { calls++; throw new InvalidOperationException("no Host call expected"); });
        var response = await service.CreateAsync(
            new SurfaceCreateMissionConversationRequest(session.Id, missionId, Guid.NewGuid(), displayed.MissionVersionId), CancellationToken.None);

        Assert.Equal(0, calls);
        Assert.Null(response.Created);
        Assert.Equal(ProjectOperationErrorCode.VersionChanged, response.Error!.Code);
    }

    [Fact]
    public async Task Create_PinsTheResolvedApprovedVersion_AndReturnsExactlyWhatHostPinned()
    {
        var (project, missionId, approved) = await CreateApprovedAsync();
        var commandId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        HostCreateMissionConversationRequest? captured = null;
        var (service, session) = SurfaceService(project.Home, async request =>
        {
            var body = await request.Content!.ReadAsStringAsync();
            Assert.DoesNotContain(project.Home, body, StringComparison.Ordinal);
            captured = JsonSerializer.Deserialize(body, ConversationContractsJsonContext.Default.CreateMissionConversationRequest);
            return Json(new HostCreateMissionConversationResponse(conversationId, 1, captured!.Launch),
                ConversationContractsJsonContext.Default.CreateMissionConversationResponse, HttpStatusCode.Created);
        });

        var response = await service.CreateAsync(
            new SurfaceCreateMissionConversationRequest(session.Id, missionId, commandId, approved.MissionVersionId), CancellationToken.None);

        Assert.Null(response.Error);
        Assert.Equal(commandId, captured!.CommandId);
        Assert.Equal(approved.MissionVersionId, captured.Launch.MissionVersionId);
        var created = response.Created!;
        Assert.Equal(conversationId, created.ConversationId);
        Assert.Equal(approved.MissionVersionId, created.Approval.MissionVersionId);
        Assert.Equal(approved.VersionNumber, created.Approval.VersionNumber);
        Assert.Equal(approved.DefinitionHash, created.Approval.DefinitionHash);
        Assert.Equal(approved.CapabilityProfile, created.Approval.Profile);
        // The approval a surface echoes back carries identity only — never the executable package.
        Assert.DoesNotContain("package", JsonSerializer.Serialize(response, ApplicationJsonContext.Default.CreateMissionConversationResponse), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RefusesAMissionWithNoApprovedVersion_AndCallsNoHost()
    {
        var project = CreatePackagedProject();
        var draft = await versions.CreateDraftAsync(project.Home, "Unpublished", Source, MissionHandsProfile.NoHands, CancellationToken.None);
        var calls = 0;
        var (service, session) = SurfaceService(project.Home, _ => { calls++; throw new InvalidOperationException("no Host call expected"); });

        var response = await service.CreateAsync(
            new SurfaceCreateMissionConversationRequest(session.Id, draft.MissionId, Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(0, calls);
        Assert.Null(response.Created);
        Assert.NotNull(response.Error);
    }

    [Fact]
    public async Task SurfaceActions_RefuseAForeignSession_WithoutTouchingProjectOrHost()
    {
        var project = CreatePackagedProject();
        var calls = 0;
        var (service, _) = SurfaceService(project.Home, _ => { calls++; throw new InvalidOperationException("no Host call expected"); });
        var foreign = Guid.NewGuid().ToString("N");

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ListAsync(new SurfaceListMissionConversationsRequest(foreign), CancellationToken.None));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ListApprovedVersionsAsync(new ListApprovedMissionVersionsRequest(foreign), CancellationToken.None));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateAsync(
            new SurfaceCreateMissionConversationRequest(foreign, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task MissionChatRows_CarryTheHostTitleAndTheProjectsOwnVersionLabel()
    {
        // Phase 48: Host owns the durable title and update order; Projects owns the name and the release
        // label. The row joins them and invents neither.
        // The real shipped release, in a Project holding the shipped pair its package is built from:
        // only that exact release carries a label, which is what the row is asserting.
        var created = projects.CreateManaged(Guid.NewGuid(), "Amber Harbor", "Chat with Forge's shipped missions.");
        var project = await projects.EnsureManagedChatAssetsAsync(created.Home, CancellationToken.None);
        var shipped = await versions.EnsureShippedApprovedVersionAsync(project.Home, ShippedMissionCatalog.MissionName,
            ShippedMissionCatalog.Definition, ShippedMissionCatalog.Profile, ShippedMissionCatalog.ReleaseLabel, CancellationToken.None);
        var approved = Assert.Single(shipped.Versions!);
        var launch = new DurableMissionLaunch(approved.MissionVersionId, approved.VersionNumber, approved.DefinitionHash,
            approved.DefinitionText, approved.CapabilityProfile, approved.Package);
        var named = new MissionConversationSummary(Guid.NewGuid(), project.Manifest.ProjectId, launch,
            ConversationRunStatus.Queued, 4, DateTimeOffset.UnixEpoch.AddMinutes(2), "Draft the rollout");
        var fresh = new MissionConversationSummary(Guid.NewGuid(), project.Manifest.ProjectId, launch,
            ConversationRunStatus.Queued, 0, DateTimeOffset.UnixEpoch, "New chat");
        var service = Service(_ => Task.FromResult(Json(new HostListMissionConversationsResponse([fresh, named]),
            ConversationContractsJsonContext.Default.ListMissionConversationsResponse, HttpStatusCode.OK)));

        var rows = await service.ListChatRowsAsync(project.Home, CancellationToken.None);

        // Newest first, named by Host, labelled by Projects.
        Assert.Equal(["Draft the rollout", "New chat"], rows.Select(row => row.Title));
        Assert.Equal(["Janus", "Janus"], rows.Select(row => row.MissionName));
        Assert.Equal(["1.4", "1.4"], rows.Select(row => row.VersionLabel));
        Assert.Equal([true, false], rows.Select(row => row.HasMessages));
    }

    [Fact]
    public async Task AMissionChatRow_StillCarriesATitle_WhenThisProjectNoLongerHoldsItsPinnedVersion()
    {
        var project = CreatePackagedProject();
        var unknown = new DurableMissionLaunch(Guid.NewGuid(), 7, "sha256:unknown", "definition", MissionHandsProfile.NoHands, null);
        var summary = new MissionConversationSummary(Guid.NewGuid(), project.Manifest.ProjectId, unknown,
            ConversationRunStatus.Queued, 2, DateTimeOffset.UnixEpoch, "Draft the rollout");
        var service = Service(_ => Task.FromResult(Json(new HostListMissionConversationsResponse([summary]),
            ConversationContractsJsonContext.Default.ListMissionConversationsResponse, HttpStatusCode.OK)));

        var row = Assert.Single(await service.ListChatRowsAsync(project.Home, CancellationToken.None));

        Assert.Equal("Draft the rollout", row.Title);
        Assert.Null(row.MissionName);
        Assert.Null(row.VersionLabel);
        Assert.Equal(7, row.VersionNumber);
    }

    private async Task<MissionVersion> PublishNextAsync(string home, Guid missionId)
    {
        var draft = await versions.CreateNextDraftAsync(home, missionId, Source + "\n", MissionHandsProfile.NoHands, CancellationToken.None);
        var candidate = await versions.PromoteCandidateAsync(home, missionId, draft.DraftId, draft.Revision, CancellationToken.None);
        var evaluationCase = new EvaluationCase(Guid.NewGuid(), "input", "", "", EvaluationOutcome.Succeeded, [], [], 1);
        candidate = await versions.SaveCaseAsync(home, missionId, candidate.MissionVersionId, evaluationCase, CancellationToken.None);
        await versions.RecordCompletionAsync(home, missionId, candidate.MissionVersionId, evaluationCase.EvaluationCaseId,
            candidate.CandidateRevision, candidate.DefinitionHash, EvaluationOutcome.Succeeded, "observed", null, CancellationToken.None);
        return await versions.PublishAsync(home, missionId, candidate.MissionVersionId, CancellationToken.None);
    }

    private (IMissionConversationService Service, ApplicationSession Session) SurfaceService(
        string home, Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
    {
        var sessions = new ApplicationSessionService(CapabilityAuthorizationPolicy.Default, _ => { }, CancellationToken.None);
        var session = sessions.CreateForProject(home);
        return (new MissionConversationService(versions, new SingleClientFactory(new DelegateHandler(send)), sessions), session);
    }

    private async Task<(ProjectRecord Project, Guid MissionId, MissionVersion Approved)> CreateApprovedAsync()
    {
        var (project, missionId, candidate, evaluationCase) = await CreateCandidateAsync();
        await versions.RecordCompletionAsync(project.Home, missionId, candidate.MissionVersionId, evaluationCase.EvaluationCaseId,
            candidate.CandidateRevision, candidate.DefinitionHash, EvaluationOutcome.Succeeded, "observed", null, CancellationToken.None);
        var approved = await versions.PublishAsync(project.Home, missionId, candidate.MissionVersionId, CancellationToken.None);
        return (project, missionId, approved);
    }

    private async Task<(ProjectRecord Project, Guid MissionId, MissionVersion Candidate, EvaluationCase EvaluationCase)> CreateCandidateAsync()
    {
        var project = CreatePackagedProject();
        var draft = await versions.CreateDraftAsync(project.Home, "Review", Source, MissionHandsProfile.NoHands, CancellationToken.None);
        var candidate = await versions.PromoteCandidateAsync(project.Home, draft.MissionId, draft.Draft!.DraftId, draft.Draft.Revision, CancellationToken.None);
        var evaluationCase = new EvaluationCase(Guid.NewGuid(), "input", "", "", EvaluationOutcome.Succeeded, [], [], 1);
        candidate = await versions.SaveCaseAsync(project.Home, draft.MissionId, candidate.MissionVersionId, evaluationCase, CancellationToken.None);
        return (project, draft.MissionId, candidate, evaluationCase);
    }

    private ProjectRecord CreatePackagedProject()
    {
        var project = projects.Create("Conversation test", null, null);
        Directory.CreateDirectory(Path.Combine(project.Home, "experts", "Researcher"));
        File.WriteAllText(Path.Combine(project.Home, "mission.mcl"), Source);
        File.WriteAllText(Path.Combine(project.Home, "experts", "Researcher", "expert.md"), Expert);
        var expertHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Expert))).ToLowerInvariant();
        File.WriteAllText(Path.Combine(project.Home, "mcl.lock"), $"version: 1\nexperts:\n  Researcher:\n    source: local\n    path: experts/Researcher/expert.md\n    hash: {expertHash}\n");
        WriteManifest(project.Home, project.Manifest with { Assets = [
            new ProjectAssetDescriptor(ProjectAssetKind.Mission, "mission.mcl", null),
            new ProjectAssetDescriptor(ProjectAssetKind.LockFile, "mcl.lock", null),
            new ProjectAssetDescriptor(ProjectAssetKind.Expert, "experts/Researcher/expert.md", null),
        ] });
        return projects.ReadForHome(project.Home);
    }

    private MissionConversationService Service(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) =>
        new(versions, new SingleClientFactory(new DelegateHandler(send)));

    private static HttpResponseMessage Json<T>(T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type, HttpStatusCode status) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(value, type), Encoding.UTF8, "application/json") };

    private static void WriteManifest(string home, ProjectManifest manifest) =>
        File.WriteAllText(Path.Combine(home, ProjectService.ManifestFileName), JsonSerializer.Serialize(manifest, ProjectManifestJsonContext.Default.ProjectManifest));

    private const string Source = "mission Review(task) = {\n  Researcher\n}\n";
    private const string Expert = "---\nname: Researcher\nkind: llm\ninput: task\noutput: answer\n---\n{{task}}";

    public void Dispose()
    {
        if (Directory.Exists(profile)) Directory.Delete(profile, recursive: true);
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = new Uri("http://conversation-host/") };
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
}
