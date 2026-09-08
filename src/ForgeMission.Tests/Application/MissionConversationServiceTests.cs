using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ForgeMission.Application;
using ForgeMission.Application.Transport;
using ForgeMission.Conversations.Contracts;

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
        CreateMissionConversationRequest? captured = null;
        var service = Service(async request =>
        {
            Assert.Equal("/mission-conversations", request.RequestUri!.AbsolutePath);
            var body = await request.Content!.ReadAsStringAsync();
            Assert.DoesNotContain(project.Home, body, StringComparison.Ordinal);
            Assert.DoesNotContain("capabilities", body, StringComparison.OrdinalIgnoreCase);
            captured = JsonSerializer.Deserialize(body, ConversationContractsJsonContext.Default.CreateMissionConversationRequest);
            var response = new CreateMissionConversationResponse(Guid.NewGuid(), 0, captured!.Launch);
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
