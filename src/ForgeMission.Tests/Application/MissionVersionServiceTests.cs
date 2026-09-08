using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ForgeMission.Application;
using ForgeMission.Application.Transport;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Tests.Application;

public sealed class MissionVersionServiceTests : IDisposable
{
    private readonly string _profile = Path.Combine(Path.GetTempPath(), "forge-mission-versions", Guid.NewGuid().ToString("N"));
    private readonly ProjectService _projects;
    private readonly MissionVersionService _versions;

    public MissionVersionServiceTests()
    {
        _projects = new ProjectService(Path.Combine(_profile, "Forge", "Projects"));
        _versions = new MissionVersionService(_projects);
    }

    [Fact]
    public async Task V4Launch_IsPreserved_WhenAnAuthoredDraftMigratesTheManifestToV5()
    {
        var project = CreatePackagedProject();
        var legacy = new MissionVersionLaunch(Guid.NewGuid(), 1, Hash("legacy"), "legacy", MissionHandsProfile.NoHands, DateTimeOffset.UtcNow);
        WriteManifest(project.Home, project.Manifest with { SchemaVersion = 4, ApprovedMissionLaunches = [legacy] });

        var draft = await _versions.CreateDraftAsync(project.Home, "Review", Source, MissionHandsProfile.NoHands, CancellationToken.None);
        var reopened = _projects.ReadForHome(project.Home).Manifest;

        Assert.Equal(5, reopened.SchemaVersion);
        Assert.Equal(legacy, Assert.Single(reopened.ApprovedMissionLaunches!));
        Assert.Equal(draft.MissionId, Assert.Single(reopened.MissionDefinitions!).MissionId);
    }

    [Fact]
    public async Task Candidate_FreezesResolvedPackage_WhenTheWorkspaceExpertChangesLater()
    {
        var project = CreatePackagedProject();
        var draft = await _versions.CreateDraftAsync(project.Home, "Review", Source, MissionHandsProfile.ProjectWorkspace, CancellationToken.None);
        var candidate = await _versions.PromoteCandidateAsync(project.Home, draft.MissionId, draft.Draft!.DraftId, draft.Draft.Revision, CancellationToken.None);
        var before = Assert.Single(candidate.Package.ResolvedExperts).ExpertMarkdown;

        File.WriteAllText(Path.Combine(project.Home, "experts", "Researcher", "expert.md"), Expert.Replace("Research", "Changed", StringComparison.Ordinal));
        var reopened = _projects.ReadForHome(project.Home).Manifest;

        Assert.Equal(before, Assert.Single(Assert.Single(reopened.MissionDefinitions!).Versions!).Package.ResolvedExperts[0].ExpertMarkdown);
        Assert.Equal(MissionHandsProfile.ProjectWorkspace, candidate.CapabilityProfile);
    }

    [Fact]
    public async Task MultipleMissionRoots_AreTypedPackageFailureAndLeaveTheManifestUntouched()
    {
        var project = CreatePackagedProject();
        var draft = await _versions.CreateDraftAsync(project.Home, "Review", Source + "mission Second(task) = {\n  Researcher\n}\n", MissionHandsProfile.NoHands, CancellationToken.None);
        var path = Path.Combine(project.Home, ProjectService.ManifestFileName);
        var before = File.ReadAllText(path);

        var failure = await Assert.ThrowsAsync<ProjectOperationException>(() => _versions.PromoteCandidateAsync(project.Home,
            draft.MissionId, draft.Draft!.DraftId, draft.Draft.Revision, CancellationToken.None));

        Assert.Equal(ProjectOperationErrorCode.InvalidManifest, failure.Code);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public async Task Evaluation_RecordsDeterministicResult_AndOnlyAllPassesCanPublish()
    {
        var project = CreatePackagedProject();
        var draft = await _versions.CreateDraftAsync(project.Home, "Review", Source, MissionHandsProfile.NoHands, CancellationToken.None);
        var candidate = await _versions.PromoteCandidateAsync(project.Home, draft.MissionId, draft.Draft!.DraftId, draft.Draft.Revision, CancellationToken.None);
        var evaluationCase = new EvaluationCase(Guid.NewGuid(), "input", "yes", "no", EvaluationOutcome.Succeeded, ["approved"], ["denied"], 1);
        candidate = await _versions.SaveCaseAsync(project.Home, draft.MissionId, candidate.MissionVersionId, evaluationCase, CancellationToken.None);

        var result = await _versions.RecordCompletionAsync(project.Home, draft.MissionId, candidate.MissionVersionId,
            evaluationCase.EvaluationCaseId, candidate.CandidateRevision, candidate.DefinitionHash, EvaluationOutcome.Succeeded,
            "approved", null, CancellationToken.None);
        var published = await _versions.PublishAsync(project.Home, draft.MissionId, candidate.MissionVersionId, CancellationToken.None);

        Assert.Equal(EvaluationResultState.Passed, result.State);
        Assert.Equal(MissionVersionState.Approved, published.State);
        Assert.Equal(published.MissionVersionId, Assert.Single(_projects.ReadForHome(project.Home).Manifest.MissionDefinitions!).ActiveApprovedVersionId);
    }

    [Fact]
    public async Task SavingCandidate_InvalidatesOldResults_AndStaleCompletionWritesNothing()
    {
        var project = CreatePackagedProject();
        var draft = await _versions.CreateDraftAsync(project.Home, "Review", Source, MissionHandsProfile.NoHands, CancellationToken.None);
        var candidate = await _versions.PromoteCandidateAsync(project.Home, draft.MissionId, draft.Draft!.DraftId, draft.Draft.Revision, CancellationToken.None);
        var evaluationCase = new EvaluationCase(Guid.NewGuid(), "input", "", "", EvaluationOutcome.Succeeded, [], [], 1);
        candidate = await _versions.SaveCaseAsync(project.Home, draft.MissionId, candidate.MissionVersionId, evaluationCase, CancellationToken.None);
        var changed = await _versions.SaveCandidateAsync(project.Home, draft.MissionId, candidate.MissionVersionId, candidate.CandidateRevision, Source + "\n", CancellationToken.None);
        var manifestPath = Path.Combine(project.Home, ProjectService.ManifestFileName);
        var before = File.ReadAllText(manifestPath);

        var failure = await Assert.ThrowsAsync<ProjectOperationException>(() => _versions.RecordCompletionAsync(project.Home, draft.MissionId,
            changed.MissionVersionId, evaluationCase.EvaluationCaseId, candidate.CandidateRevision, candidate.DefinitionHash,
            EvaluationOutcome.Succeeded, "ok", null, CancellationToken.None));

        Assert.Equal(ProjectOperationErrorCode.VersionChanged, failure.Code);
        Assert.Equal(before, File.ReadAllText(manifestPath));
        Assert.Empty(changed.EvaluationResults!);
    }

    [Fact]
    public async Task EvaluationExecution_IsUnavailableAndDoesNotCreatePendingState()
    {
        var project = CreatePackagedProject();
        var draft = await _versions.CreateDraftAsync(project.Home, "Review", Source, MissionHandsProfile.NoHands, CancellationToken.None);
        var candidate = await _versions.PromoteCandidateAsync(project.Home, draft.MissionId, draft.Draft!.DraftId, draft.Draft.Revision, CancellationToken.None);
        var before = File.ReadAllText(Path.Combine(project.Home, ProjectService.ManifestFileName));

        var failure = await Assert.ThrowsAsync<ProjectOperationException>(() => _versions.StartEvaluationAsync(project.Home, draft.MissionId, candidate.MissionVersionId, CancellationToken.None));

        Assert.Equal(ProjectOperationErrorCode.EvaluationUnavailable, failure.Code);
        Assert.Equal(before, File.ReadAllText(Path.Combine(project.Home, ProjectService.ManifestFileName)));
    }

    [Fact]
    public async Task ANewDraftAfterApproval_PromotesTheNextVersionWithItsApprovedParent()
    {
        var project = CreatePackagedProject();
        var firstDraft = await _versions.CreateDraftAsync(project.Home, "Review", Source, MissionHandsProfile.NoHands, CancellationToken.None);
        var first = await _versions.PromoteCandidateAsync(project.Home, firstDraft.MissionId, firstDraft.Draft!.DraftId, firstDraft.Draft.Revision, CancellationToken.None);
        var evaluationCase = new EvaluationCase(Guid.NewGuid(), "input", "", "", EvaluationOutcome.Succeeded, [], [], 1);
        first = await _versions.SaveCaseAsync(project.Home, firstDraft.MissionId, first.MissionVersionId, evaluationCase, CancellationToken.None);
        await _versions.RecordCompletionAsync(project.Home, firstDraft.MissionId, first.MissionVersionId, evaluationCase.EvaluationCaseId,
            first.CandidateRevision, first.DefinitionHash, EvaluationOutcome.Succeeded, "ok", null, CancellationToken.None);
        await _versions.PublishAsync(project.Home, firstDraft.MissionId, first.MissionVersionId, CancellationToken.None);

        var nextDraft = await _versions.CreateNextDraftAsync(project.Home, firstDraft.MissionId, Source, MissionHandsProfile.ProjectWorkspace, CancellationToken.None);
        var second = await _versions.PromoteCandidateAsync(project.Home, firstDraft.MissionId, nextDraft.DraftId, nextDraft.Revision, CancellationToken.None);

        Assert.Equal(2, second.VersionNumber);
        Assert.Equal(first.MissionVersionId, second.ParentVersionId);
    }

    [Theory]
    [InlineData("Evaluated")]
    [InlineData("Approved")]
    [InlineData("Superseded")]
    public async Task TamperedTerminalLifecycleState_WithoutCurrentPassingResults_IsRejectedWithoutRewrite(string stateName)
    {
        var state = Enum.Parse<MissionVersionState>(stateName);
        var (project, definition, published) = await CreatePublishedAsync();
        var invalid = published with
        {
            State = state,
            ApprovedAtUtc = state == MissionVersionState.Evaluated ? null : published.ApprovedAtUtc,
            EvaluationResults = [],
        };
        var active = state == MissionVersionState.Approved ? invalid.MissionVersionId : (Guid?)null;
        WriteManifest(project.Home, project.Manifest with
        {
            MissionDefinitions = [definition with { ActiveApprovedVersionId = active, Versions = [invalid] }],
        });
        var path = Path.Combine(project.Home, ProjectService.ManifestFileName);
        var before = File.ReadAllText(path);

        var failure = Assert.Throws<ProjectOperationException>(() => _projects.ReadForHome(project.Home));

        Assert.Equal(ProjectOperationErrorCode.InvalidManifest, failure.Code);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public async Task MissingActiveApprovedVersion_IsRejectedWithoutRewrite()
    {
        var (project, definition, published) = await CreatePublishedAsync();
        WriteManifest(project.Home, project.Manifest with
        {
            MissionDefinitions = [definition with { ActiveApprovedVersionId = null, Versions = [published] }],
        });
        var path = Path.Combine(project.Home, ProjectService.ManifestFileName);
        var before = File.ReadAllText(path);

        var failure = Assert.Throws<ProjectOperationException>(() => _projects.ReadForHome(project.Home));

        Assert.Equal(ProjectOperationErrorCode.InvalidManifest, failure.Code);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public async Task GapOrDanglingParentLineage_IsRejectedWithoutRewrite()
    {
        var (project, definition, published) = await CreatePublishedAsync();
        var nextDraft = await _versions.CreateNextDraftAsync(project.Home, definition.MissionId, Source, MissionHandsProfile.NoHands, CancellationToken.None);
        var second = await _versions.PromoteCandidateAsync(project.Home, definition.MissionId, nextDraft.DraftId, nextDraft.Revision, CancellationToken.None);
        var current = _projects.ReadForHome(project.Home).Manifest;
        var stored = Assert.Single(current.MissionDefinitions!);
        var invalid = second with { VersionNumber = 3, ParentVersionId = Guid.NewGuid() };
        WriteManifest(project.Home, current with { MissionDefinitions = [stored with { Versions = [published, invalid] }] });
        var path = Path.Combine(project.Home, ProjectService.ManifestFileName);
        var before = File.ReadAllText(path);

        var failure = Assert.Throws<ProjectOperationException>(() => _projects.ReadForHome(project.Home));

        Assert.Equal(ProjectOperationErrorCode.InvalidManifest, failure.Code);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public async Task CrossMissionParentLineage_IsRejectedWithoutRewrite()
    {
        var (project, firstDefinition, published) = await CreatePublishedAsync();
        var other = await _versions.CreateDraftAsync(project.Home, "Other", Source, MissionHandsProfile.NoHands, CancellationToken.None);
        var second = await _versions.PromoteCandidateAsync(project.Home, other.MissionId, other.Draft!.DraftId, other.Draft.Revision, CancellationToken.None);
        var current = _projects.ReadForHome(project.Home).Manifest;
        var definitions = current.MissionDefinitions!;
        var otherDefinition = definitions.Single(item => item.MissionId == other.MissionId);
        WriteManifest(project.Home, current with
        {
            MissionDefinitions = [
                definitions.Single(item => item.MissionId == firstDefinition.MissionId),
                otherDefinition with { Versions = [second with { ParentVersionId = published.MissionVersionId }] },
            ],
        });
        var path = Path.Combine(project.Home, ProjectService.ManifestFileName);
        var before = File.ReadAllText(path);

        var failure = Assert.Throws<ProjectOperationException>(() => _projects.ReadForHome(project.Home));

        Assert.Equal(ProjectOperationErrorCode.InvalidManifest, failure.Code);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public async Task SelfParentLineage_IsRejectedWithoutRewrite()
    {
        var (project, definition, published) = await CreatePublishedAsync();
        var nextDraft = await _versions.CreateNextDraftAsync(project.Home, definition.MissionId, Source, MissionHandsProfile.NoHands, CancellationToken.None);
        var second = await _versions.PromoteCandidateAsync(project.Home, definition.MissionId, nextDraft.DraftId, nextDraft.Revision, CancellationToken.None);
        var current = _projects.ReadForHome(project.Home).Manifest;
        var stored = Assert.Single(current.MissionDefinitions!);
        var invalid = second with { ParentVersionId = second.MissionVersionId };
        WriteManifest(project.Home, current with { MissionDefinitions = [stored with { Versions = [published, invalid] }] });
        var path = Path.Combine(project.Home, ProjectService.ManifestFileName);
        var before = File.ReadAllText(path);

        var failure = Assert.Throws<ProjectOperationException>(() => _projects.ReadForHome(project.Home));

        Assert.Equal(ProjectOperationErrorCode.InvalidManifest, failure.Code);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public async Task MissingSecondVersionParent_IsRejectedWithoutRewrite()
    {
        var (project, definition, published) = await CreatePublishedAsync();
        var nextDraft = await _versions.CreateNextDraftAsync(project.Home, definition.MissionId, Source, MissionHandsProfile.NoHands, CancellationToken.None);
        var second = await _versions.PromoteCandidateAsync(project.Home, definition.MissionId, nextDraft.DraftId, nextDraft.Revision, CancellationToken.None);
        var current = _projects.ReadForHome(project.Home).Manifest;
        var stored = Assert.Single(current.MissionDefinitions!);
        WriteManifest(project.Home, current with { MissionDefinitions = [stored with { Versions = [published, second with { ParentVersionId = null }] }] });
        var path = Path.Combine(project.Home, ProjectService.ManifestFileName);
        var before = File.ReadAllText(path);

        var failure = Assert.Throws<ProjectOperationException>(() => _projects.ReadForHome(project.Home));

        Assert.Equal(ProjectOperationErrorCode.InvalidManifest, failure.Code);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public async Task ThirdVersionSkippingItsImmediateParent_IsRejectedWithoutRewrite()
    {
        var (project, definition, published) = await CreatePublishedAsync();
        var secondDraft = await _versions.CreateNextDraftAsync(project.Home, definition.MissionId, Source, MissionHandsProfile.NoHands, CancellationToken.None);
        var second = await _versions.PromoteCandidateAsync(project.Home, definition.MissionId, secondDraft.DraftId, secondDraft.Revision, CancellationToken.None);
        var thirdDraft = await _versions.CreateNextDraftAsync(project.Home, definition.MissionId, Source, MissionHandsProfile.NoHands, CancellationToken.None);
        var third = await _versions.PromoteCandidateAsync(project.Home, definition.MissionId, thirdDraft.DraftId, thirdDraft.Revision, CancellationToken.None);
        Assert.Equal(second.MissionVersionId, third.ParentVersionId);
        var current = _projects.ReadForHome(project.Home).Manifest;
        var stored = Assert.Single(current.MissionDefinitions!);
        WriteManifest(project.Home, current with { MissionDefinitions = [stored with { Versions = [published, second, third with { ParentVersionId = published.MissionVersionId }] }] });
        var path = Path.Combine(project.Home, ProjectService.ManifestFileName);
        var before = File.ReadAllText(path);

        var failure = Assert.Throws<ProjectOperationException>(() => _projects.ReadForHome(project.Home));

        Assert.Equal(ProjectOperationErrorCode.InvalidManifest, failure.Code);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public async Task ConcurrentPublish_HasOneApprovalAndOneTypedConflict()
    {
        var project = CreatePackagedProject();
        var draft = await _versions.CreateDraftAsync(project.Home, "Review", Source, MissionHandsProfile.NoHands, CancellationToken.None);
        var candidate = await _versions.PromoteCandidateAsync(project.Home, draft.MissionId, draft.Draft!.DraftId, draft.Draft.Revision, CancellationToken.None);
        var evaluationCase = new EvaluationCase(Guid.NewGuid(), "input", "", "", EvaluationOutcome.Succeeded, [], [], 1);
        candidate = await _versions.SaveCaseAsync(project.Home, draft.MissionId, candidate.MissionVersionId, evaluationCase, CancellationToken.None);
        await _versions.RecordCompletionAsync(project.Home, draft.MissionId, candidate.MissionVersionId, evaluationCase.EvaluationCaseId,
            candidate.CandidateRevision, candidate.DefinitionHash, EvaluationOutcome.Succeeded, "ok", null, CancellationToken.None);
        var rival = new MissionVersionService(new ProjectService(Path.Combine(_profile, "Forge", "Projects")));

        var first = _versions.PublishAsync(project.Home, draft.MissionId, candidate.MissionVersionId, CancellationToken.None);
        var second = rival.PublishAsync(project.Home, draft.MissionId, candidate.MissionVersionId, CancellationToken.None);
        var results = await Task.WhenAll(AsResult(first), AsResult(second));

        Assert.Single(results, result => result.Error is null);
        Assert.Single(results, result => result.Error?.Code == ProjectOperationErrorCode.PublishConflict);
        Assert.Equal(MissionVersionState.Approved, Assert.Single(Assert.Single(_projects.ReadForHome(project.Home).Manifest.MissionDefinitions!).Versions!).State);
    }

    [Fact]
    public async Task TamperedProfile_IsRejectedWithoutRewrite()
    {
        var project = CreatePackagedProject();
        var draft = await _versions.CreateDraftAsync(project.Home, "Review", Source, MissionHandsProfile.NoHands, CancellationToken.None);
        var path = Path.Combine(project.Home, ProjectService.ManifestFileName);
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"noHands\"", "\"unbounded\"", StringComparison.Ordinal));
        var before = File.ReadAllText(path);

        var failure = Assert.Throws<ProjectOperationException>(() => _projects.ReadForHome(project.Home));

        Assert.Equal(ProjectOperationErrorCode.InvalidManifest, failure.Code);
        Assert.Equal(before, File.ReadAllText(path));
    }

    private async Task<(ProjectRecord Project, ProjectMissionDefinition Definition, MissionVersion Published)> CreatePublishedAsync()
    {
        var project = CreatePackagedProject();
        var draft = await _versions.CreateDraftAsync(project.Home, "Review", Source, MissionHandsProfile.NoHands, CancellationToken.None);
        var candidate = await _versions.PromoteCandidateAsync(project.Home, draft.MissionId, draft.Draft!.DraftId, draft.Draft.Revision, CancellationToken.None);
        var evaluationCase = new EvaluationCase(Guid.NewGuid(), "input", "", "", EvaluationOutcome.Succeeded, [], [], 1);
        candidate = await _versions.SaveCaseAsync(project.Home, draft.MissionId, candidate.MissionVersionId, evaluationCase, CancellationToken.None);
        await _versions.RecordCompletionAsync(project.Home, draft.MissionId, candidate.MissionVersionId, evaluationCase.EvaluationCaseId,
            candidate.CandidateRevision, candidate.DefinitionHash, EvaluationOutcome.Succeeded, "ok", null, CancellationToken.None);
        var published = await _versions.PublishAsync(project.Home, draft.MissionId, candidate.MissionVersionId, CancellationToken.None);
        var definition = Assert.Single(_projects.ReadForHome(project.Home).Manifest.MissionDefinitions!);
        return (project, definition, published);
    }

    private static async Task<(MissionVersion? Value, ProjectOperationException? Error)> AsResult(Task<MissionVersion> operation)
    {
        try { return (await operation, null); }
        catch (ProjectOperationException exception) { return (null, exception); }
    }

    private ProjectRecord CreatePackagedProject()
    {
        var project = _projects.Create("Version test", null, null);
        Directory.CreateDirectory(Path.Combine(project.Home, "experts", "Researcher"));
        File.WriteAllText(Path.Combine(project.Home, "mission.mcl"), Source);
        File.WriteAllText(Path.Combine(project.Home, "experts", "Researcher", "expert.md"), Expert);
        var expertHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Expert))).ToLowerInvariant();
        File.WriteAllText(Path.Combine(project.Home, "mcl.lock"), $"version: 1\nexperts:\n  Researcher:\n    source: local\n    path: experts/Researcher/expert.md\n    hash: {expertHash}\n");
        WriteManifest(project.Home, project.Manifest with
        {
            Assets = [
                new ProjectAssetDescriptor(ProjectAssetKind.Mission, "mission.mcl", null),
                new ProjectAssetDescriptor(ProjectAssetKind.LockFile, "mcl.lock", null),
                new ProjectAssetDescriptor(ProjectAssetKind.Expert, "experts/Researcher/expert.md", null),
            ],
        });
        return _projects.ReadForHome(project.Home);
    }

    private static void WriteManifest(string home, ProjectManifest manifest) =>
        File.WriteAllText(Path.Combine(home, ProjectService.ManifestFileName), JsonSerializer.Serialize(manifest, ProjectManifestJsonContext.Default.ProjectManifest));
    private static string Hash(string value) => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private const string Source = "mission Review(task) = {\n  Researcher\n}\n";
    private const string Expert = "---\nname: Researcher\nkind: llm\ninput: task\noutput: answer\n---\n{{task}}";

    public void Dispose()
    {
        if (Directory.Exists(_profile)) Directory.Delete(_profile, recursive: true);
    }
}
