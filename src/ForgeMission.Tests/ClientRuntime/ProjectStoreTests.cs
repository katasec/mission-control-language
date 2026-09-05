using System.Diagnostics;
using System.Text.Json;
using ForgeMission.ClientRuntime.Services;
using ForgeMission.ClientRuntime.Transport;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Tests.ClientRuntime;

/// <summary>
/// Phase 43.20 Task 1 — every Project rule lives here, so every rule is asserted here: derivation,
/// the collision-safe write, manifest validation, and the draft's total absence of side effects.
/// No Presentation type appears in this file; a TUI would exercise the same class the same way.
/// </summary>
public sealed class ProjectStoreTests : IDisposable
{
    private readonly string _profile = Directory.CreateTempSubdirectory("forge-project-store-").FullName;
    private readonly ProjectStore _store;

    public ProjectStoreTests() => _store = new ProjectStore(ProjectsRoot);

    private string ProjectsRoot => Path.Combine(_profile, "Forge", "Projects");

    public void Dispose() => Directory.Delete(_profile, recursive: true);

    // --- draft is pure ---------------------------------------------------------------------

    [Fact]
    public void Draft_DerivesTitleAndHome_AndTouchesNothingOnDisk()
    {
        var draft = _store.Draft("Todos API", null, null);

        Assert.Equal("Todos API", draft.ProposedTitle);
        Assert.Equal(Path.Combine(ProjectsRoot, "todos-api"), draft.HomePath);
        Assert.False(Directory.Exists(ProjectsRoot));
    }

    [Fact]
    public void Draft_HonoursTitleAndHomeOverrides()
    {
        var elsewhere = Path.Combine(_profile, "elsewhere");

        var draft = _store.Draft("build a todos API", "Renamed", elsewhere);

        Assert.Equal("Renamed", draft.ProposedTitle);
        Assert.Equal(elsewhere, draft.HomePath);
        Assert.False(Directory.Exists(elsewhere));
    }

    [Fact]
    public void Draft_EmptyGoal_IsRejected()
    {
        var failure = Assert.Throws<ProjectOperationException>(() => _store.Draft("   ", null, null));

        Assert.Equal(ProjectOperationErrorCode.InvalidGoal, failure.Code);
    }

    // A title override says what to call the Project, never that it may exist without a goal.
    // Returning the override before the goal gate is exactly how an empty goal reached a
    // persisted manifest.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t")]
    public void Draft_EmptyGoal_IsRejectedEvenWithANonEmptyTitleOverride(string goal)
    {
        var failure = Assert.Throws<ProjectOperationException>(
            () => _store.Draft(goal, "Todos API", null));

        Assert.Equal(ProjectOperationErrorCode.InvalidGoal, failure.Code);
    }

    [Fact]
    public void Draft_EmptyGoal_IsRejectedWithBothATitleAndAHomeOverride()
    {
        var failure = Assert.Throws<ProjectOperationException>(
            () => _store.Draft("  ", "Todos API", Path.Combine(_profile, "work")));

        Assert.Equal(ProjectOperationErrorCode.InvalidGoal, failure.Code);
    }

    [Fact]
    public void Draft_RelativeHomeOverride_IsRejected()
    {
        var failure = Assert.Throws<ProjectOperationException>(
            () => _store.Draft("Todos API", null, "relative/path"));

        Assert.Equal(ProjectOperationErrorCode.InvalidHome, failure.Code);
    }

    // The locked draft-vs-create divergence: a draft never probes for a collision (that would be
    // both filesystem access and an implied reservation), so it keeps showing the base home while
    // create — the authoritative writer — moves to the next free suffix.
    [Fact]
    public void Draft_AfterTheBaseHomeIsTaken_StillShowsTheBaseHome_WhileCreateMovesOn()
    {
        _store.Create("Todos API", null, null);

        var draft = _store.Draft("Todos API", null, null);
        var second = _store.Create("Todos API", null, null);

        Assert.Equal(Path.Combine(ProjectsRoot, "todos-api"), draft.HomePath);
        Assert.Equal(Path.Combine(ProjectsRoot, "todos-api-2"), second.Home);
    }

    // --- derivation ------------------------------------------------------------------------

    [Theory]
    [InlineData("Todos API", "Todos API", "todos-api")]
    [InlineData("  Build a Todos API  ", "Build a Todos API", "build-a-todos-api")]
    [InlineData("Ship it!\nThen tell me", "Ship it!", "ship-it")]
    [InlineData("***", "***", "project")]
    [InlineData("日本語", "日本語", "project")]
    public void Derivation_ProducesTheTitleAndSlug(string goal, string expectedTitle, string expectedSlug)
    {
        var draft = _store.Draft(goal, null, null);

        Assert.Equal(expectedTitle, draft.ProposedTitle);
        Assert.Equal(Path.Combine(ProjectsRoot, expectedSlug), draft.HomePath);
    }

    [Fact]
    public void Derivation_TruncatesALongTitleAtAWordBoundary()
    {
        var goal = string.Join(' ', Enumerable.Repeat("alpha", 20));

        var draft = _store.Draft(goal, null, null);

        Assert.True(draft.ProposedTitle.Length <= 60);
        Assert.EndsWith("alpha", draft.ProposedTitle, StringComparison.Ordinal);
        Assert.DoesNotContain("alph ", draft.ProposedTitle, StringComparison.Ordinal);
    }

    // --- create ----------------------------------------------------------------------------

    [Fact]
    public void Create_WritesTheCompleteV3Manifest()
    {
        var created = _store.Create("Todos API", null, null);

        var manifest = created.Manifest;
        Assert.Equal(3, manifest.SchemaVersion);
        Assert.NotEqual(Guid.Empty, manifest.ProjectId);
        Assert.Equal("Todos API", manifest.Title);
        Assert.Equal("Todos API", manifest.Goal);
        Assert.Empty(manifest.Assets);
        Assert.Empty(manifest.AttachedContext);
        Assert.Empty(manifest.Runs);
        Assert.Null(manifest.LegacyProjectControlConversationId);
        Assert.Null(manifest.ProjectMissionContainerId);
        Assert.Null(manifest.Submission);
        Assert.Equal(ProjectMissionOrigin.BuiltIn, manifest.SelectedMission.Origin);
        Assert.Equal("Janus", manifest.SelectedMission.Reference);
        Assert.Null(manifest.SelectedMission.Digest);
        Assert.True(File.Exists(Path.Combine(created.Home, ProjectStore.ManifestFileName)));
    }

    [Fact]
    public void Create_WritesTheManifestAsReadableCamelCaseJson()
    {
        var created = _store.Create("Todos API", null, null);

        var json = File.ReadAllText(Path.Combine(created.Home, ProjectStore.ManifestFileName));

        Assert.Contains("\"schemaVersion\": 3", json, StringComparison.Ordinal);
        Assert.Contains("\"legacyProjectControlConversationId\": null", json, StringComparison.Ordinal);
        Assert.DoesNotContain("missionControlConversationId", json, StringComparison.Ordinal);
        Assert.Contains("\"origin\": \"BuiltIn\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_EmptyGoal_IsRejectedAndCreatesNothing()
    {
        var failure = Assert.Throws<ProjectOperationException>(() => _store.Create("  ", null, null));

        Assert.Equal(ProjectOperationErrorCode.InvalidGoal, failure.Code);
        Assert.False(Directory.Exists(ProjectsRoot));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t")]
    public void Create_EmptyGoal_IsRejectedEvenWithANonEmptyTitleOverride_AndCreatesNothing(string goal)
    {
        var failure = Assert.Throws<ProjectOperationException>(
            () => _store.Create(goal, "Todos API", null));

        Assert.Equal(ProjectOperationErrorCode.InvalidGoal, failure.Code);
        Assert.False(Directory.Exists(ProjectsRoot));
    }

    // The explicit-home branch is the one an empty goal could have reached a manifest through:
    // it skips slug derivation entirely, so it needs its own proof that the gate still fires.
    [Fact]
    public void Create_EmptyGoal_IsRejectedWithATitleAndHomeOverride_AndWritesNoManifest()
    {
        var chosen = Path.Combine(_profile, "work", "todos");

        var failure = Assert.Throws<ProjectOperationException>(
            () => _store.Create("   ", "Todos API", chosen));

        Assert.Equal(ProjectOperationErrorCode.InvalidGoal, failure.Code);
        Assert.False(File.Exists(Path.Combine(chosen, ProjectStore.ManifestFileName)));
    }

    [Fact]
    public void Create_CollidingTitles_TakeTheNextSuffix_AndLeaveEarlierManifestsIntact()
    {
        var first = _store.Create("Todos API", null, null);
        var second = _store.Create("Todos API", null, null);
        var third = _store.Create("Todos API", null, null);

        Assert.Equal(Path.Combine(ProjectsRoot, "todos-api"), first.Home);
        Assert.Equal(Path.Combine(ProjectsRoot, "todos-api-2"), second.Home);
        Assert.Equal(Path.Combine(ProjectsRoot, "todos-api-3"), third.Home);
        Assert.NotEqual(first.Manifest.ProjectId, second.Manifest.ProjectId);
        Assert.Equal(first.Manifest.ProjectId, _store.Open(first.Home).Project!.Manifest.ProjectId);
    }

    [Fact]
    public void Create_IntoAnExplicitHome_UsesThatExactDirectory()
    {
        var chosen = Path.Combine(_profile, "work", "todos");

        var created = _store.Create("Todos API", null, chosen);

        Assert.Equal(chosen, created.Home);
        Assert.False(Directory.Exists(ProjectsRoot));
    }

    // A home outside Forge's projects root is a directory the person named themselves: silently
    // relocating it would be worse than refusing, so there a collision is a failure, not a suffix.
    [Fact]
    public void Create_IntoAnOutsideDirectoryThatIsAlreadyAProject_IsRejected()
    {
        var outside = Path.Combine(_profile, "work", "todos");
        _store.Create("Todos API", null, outside);

        var failure = Assert.Throws<ProjectOperationException>(
            () => _store.Create("Todos API", null, outside));

        Assert.Equal(ProjectOperationErrorCode.InvalidHome, failure.Code);
    }

    // A confirmed draft sends the derived home straight back, so create must still pick a free
    // name inside its own projects root — otherwise the collision path would be unreachable from
    // any surface that shows the draft before confirming it.
    [Fact]
    public void Create_IntoADraftedHomeInsideTheProjectsRoot_TakesTheNextFreeSuffix()
    {
        var first = _store.Create("Todos API", null, null);
        var draft = _store.Draft("Todos API", null, null);

        var second = _store.Create("Todos API", "Todos API", draft.HomePath);

        Assert.Equal(first.Home, draft.HomePath);
        Assert.Equal(Path.Combine(ProjectsRoot, "todos-api-2"), second.Home);
        Assert.NotEqual(first.Manifest.ProjectId, second.Manifest.ProjectId);
    }

    // --- open ------------------------------------------------------------------------------

    [Fact]
    public void Open_AnExistingProject_PreservesItsIdentity()
    {
        var created = _store.Create("Todos API", null, null);

        var opened = _store.Open(created.Home).Project;

        Assert.NotNull(opened);
        Assert.Equal(created.Manifest.ProjectId, opened.Manifest.ProjectId);
        Assert.Equal(created.Home, opened.Home);
    }

    [Fact]
    public void Open_ADirectoryWithoutAManifest_ProposesItAndCreatesNothing()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_profile, "existing-code")).FullName;

        var result = _store.Open(folder);

        Assert.Null(result.Project);
        Assert.Equal(folder, result.GoalRequired!.HomePath);
        Assert.Equal("existing-code", result.GoalRequired.ProposedTitle);
        Assert.Empty(Directory.EnumerateFileSystemEntries(folder));
    }

    [Fact]
    public void Open_ADirectoryThatDoesNotExist_IsRejectedWithoutCreatingIt()
    {
        var missing = Path.Combine(_profile, "not-here");

        var failure = Assert.Throws<ProjectOperationException>(() => _store.Open(missing));

        Assert.Equal(ProjectOperationErrorCode.HomeNotFound, failure.Code);
        Assert.False(Directory.Exists(missing));
    }

    // --- manifest validation -----------------------------------------------------------------

    [Fact]
    public void Open_ANewerSchemaVersion_IsRefusedAndLeftUntouched()
    {
        var home = WriteManifest("""
            { "schemaVersion": 4, "projectId": "f0a1b2c3-0000-0000-0000-000000000001",
              "title": "Future", "goal": "later", "selectedMission": { "origin": "BuiltIn", "reference": "Janus" } }
            """);

        AssertRefused(home, ProjectOperationErrorCode.UnsupportedManifestVersion);
    }

    [Fact]
    public void Open_MalformedJson_IsRefusedAndLeftUntouched()
    {
        AssertRefused(WriteManifest("{ not json"), ProjectOperationErrorCode.InvalidManifest);
    }

    [Fact]
    public void ManifestFile_ReadFailure_IsMappedWithoutCallingItMissing()
    {
        var home = Directory.CreateDirectory(Path.Combine(_profile, "unreadable", Guid.NewGuid().ToString("N"))).FullName;
        Directory.CreateDirectory(Path.Combine(home, ProjectStore.ManifestFileName));

        var failure = Assert.Throws<ProjectOperationException>(() => new ProjectManifestFile().Read(home));

        Assert.Equal(ProjectOperationErrorCode.ManifestReadFailed, failure.Code);
    }

    [Fact]
    public void Open_AManifestMissingARequiredField_IsRefusedAndLeftUntouched()
    {
        var home = WriteManifest("""
            { "schemaVersion": 1, "projectId": "f0a1b2c3-0000-0000-0000-000000000002",
              "title": "No goal", "selectedMission": { "origin": "BuiltIn", "reference": "Janus" } }
            """);

        AssertRefused(home, ProjectOperationErrorCode.InvalidManifest);
    }

    [Fact]
    public void Open_AnAssetPathThatEscapesTheProjectHome_IsRefused()
    {
        var home = WriteManifest("""
            { "schemaVersion": 1, "projectId": "f0a1b2c3-0000-0000-0000-000000000003",
              "title": "Escaping", "goal": "escape", "selectedMission": { "origin": "BuiltIn", "reference": "Janus" },
              "assets": [ { "kind": "Mission", "relativePath": "../../outside.mcl" } ] }
            """);

        AssertRefused(home, ProjectOperationErrorCode.InvalidPath);
    }

    [Fact]
    public void Open_AnArtifactContextCarryingALocalPath_IsRefused()
    {
        var home = WriteManifest("""
            { "schemaVersion": 1, "projectId": "f0a1b2c3-0000-0000-0000-000000000004",
              "title": "Leaky", "goal": "leak", "selectedMission": { "origin": "BuiltIn", "reference": "Janus" },
              "attachedContext": [ { "id": "c1", "kind": "Artifact", "displayName": "a", "reference": "/tmp/local" } ] }
            """);

        AssertRefused(home, ProjectOperationErrorCode.InvalidPath);
    }

    // A manifest with no collections at all is an older-but-valid hand edit, not a failure: the
    // identity fields are what a Project cannot be without.
    [Fact]
    public void Open_AManifestWithoutCollections_ReadsThemAsEmpty()
    {
        var home = WriteManifest("""
            { "schemaVersion": 1, "projectId": "f0a1b2c3-0000-0000-0000-000000000005",
              "title": "Sparse", "goal": "sparse", "selectedMission": { "origin": "BuiltIn", "reference": "Janus" } }
            """);

        var manifest = _store.Open(home).Project!.Manifest;

        Assert.Empty(manifest.Assets);
        Assert.Empty(manifest.AttachedContext);
        Assert.Empty(manifest.Runs);
    }

    // --- v3 migration and immutable submission journal -----------------------------------------

    [Fact]
    public async Task OpeningV1_IsNonMutating_AndTheNextMutationPublishesV3WithLegacyHistory()
    {
        var legacyId = Guid.NewGuid();
        var home = WriteManifest($$"""
            { "schemaVersion": 1, "projectId": "{{Guid.NewGuid()}}", "title": "Legacy", "goal": "keep history",
              "selectedMission": { "origin": "BuiltIn", "reference": "Janus" },
              "missionControlConversationId": "{{legacyId}}" }
            """);
        var manifestPath = Path.Combine(home, ProjectStore.ManifestFileName);
        var before = File.ReadAllText(manifestPath);

        var opened = _store.Open(home).Project!.Manifest;

        Assert.Equal(3, opened.SchemaVersion);
        Assert.Equal(legacyId, opened.LegacyProjectControlConversationId);
        Assert.Equal(before, File.ReadAllText(manifestPath));

        await _store.SelectMissionAsync(home, "Naive", CancellationToken.None);

        var rewritten = File.ReadAllText(manifestPath);
        Assert.Contains("\"schemaVersion\": 3", rewritten, StringComparison.Ordinal);
        Assert.Contains(legacyId.ToString(), rewritten, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("missionControlConversationId", rewritten, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpeningV2_PreservesContainerAndLegacyData_WhenItsNextWritePublishesV3()
    {
        var containerId = Guid.NewGuid();
        var legacyId = Guid.NewGuid();
        var home = WriteManifest(FullyPopulatedV2ManifestJson(containerId, legacyId));

        var opened = _store.Open(home).Project!.Manifest;
        Assert.Equal(containerId, opened.ProjectMissionContainerId);
        Assert.Equal(legacyId, opened.LegacyProjectControlConversationId);

        var updated = await _store.SelectMissionAsync(home, "Janus", CancellationToken.None);

        Assert.Equal(3, updated.Manifest.SchemaVersion);
        Assert.Equal(containerId, updated.Manifest.ProjectMissionContainerId);
        Assert.Equal(legacyId, updated.Manifest.LegacyProjectControlConversationId);
        Assert.Equal(2, updated.Manifest.Assets.Length);
        Assert.Equal(2, updated.Manifest.AttachedContext.Length);
        Assert.Single(updated.Manifest.Runs);
    }

    [Fact]
    public async Task PreparedSubmission_RemainsImmutable_WhenSelectionChangesBeforeRetry()
    {
        var project = _store.Create("Ship a release", null, null);
        var commandId = Guid.NewGuid();
        var prepared = await _store.PrepareSubmissionAsync(
            project.Home, commandId, null, "write release notes", CancellationToken.None);

        await _store.SelectMissionAsync(project.Home, "Naive", CancellationToken.None);
        var retried = await _store.PrepareSubmissionAsync(
            project.Home, commandId, null, "write release notes", CancellationToken.None);

        Assert.Equal("Janus", prepared.Manifest.Submission!.Mission);
        Assert.Equal(prepared.Manifest.Submission, retried.Manifest.Submission);
        Assert.Equal("Naive", retried.Manifest.SelectedMission.Reference);
    }

    [Fact]
    public async Task PreparedSubmission_RejectsChangedContentAndASecondIntent()
    {
        var project = _store.Create("Ship a release", null, null);
        var commandId = Guid.NewGuid();
        await _store.PrepareSubmissionAsync(project.Home, commandId, null, "first", CancellationToken.None);

        var changed = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            _store.PrepareSubmissionAsync(project.Home, commandId, null, "changed", CancellationToken.None));
        var second = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            _store.PrepareSubmissionAsync(project.Home, Guid.NewGuid(), commandId, "second", CancellationToken.None));

        Assert.Equal(ProjectOperationErrorCode.MissionRunConflict, changed.Code);
        Assert.Equal(ProjectOperationErrorCode.SubmissionPending, second.Code);
        Assert.Equal("first", _store.Open(project.Home).Project!.Manifest.Submission!.Input);
    }

    [Fact]
    public async Task AcceptanceReceipt_IsIdempotent_AndPermitsTheNextDeliberateSubmission()
    {
        var project = _store.Create("Ship a release", null, null);
        var firstId = Guid.NewGuid();
        await _store.PrepareSubmissionAsync(project.Home, firstId, null, "first", CancellationToken.None);
        var acceptance = new ProjectSubmissionAcceptance(
            Guid.NewGuid(), Guid.NewGuid(), 7, ConversationRunStatus.Queued);

        var accepted = await _store.RecordSubmissionAcceptedAsync(project.Home, firstId, acceptance, CancellationToken.None);
        var repeated = await _store.RecordSubmissionAcceptedAsync(project.Home, firstId, acceptance, CancellationToken.None);
        var nextId = Guid.NewGuid();
        var next = await _store.PrepareSubmissionAsync(project.Home, nextId, firstId, "second", CancellationToken.None);

        Assert.Equal(ProjectSubmissionPhase.Accepted, accepted.Manifest.Submission!.Phase);
        Assert.Equal(accepted.Manifest.Submission, repeated.Manifest.Submission);
        Assert.Equal(nextId, next.Manifest.Submission!.CommandId);
        Assert.Equal(firstId, next.Manifest.Submission.PreviousCommandId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PrepareSubmission_RejectsBlankInputWithoutWriting(string input)
    {
        var project = _store.Create("Ship a release", null, null);
        var before = File.ReadAllText(Path.Combine(project.Home, ProjectStore.ManifestFileName));

        var failure = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            _store.PrepareSubmissionAsync(project.Home, Guid.NewGuid(), null, input, CancellationToken.None));

        Assert.Equal(ProjectOperationErrorCode.InvalidMissionInput, failure.Code);
        Assert.Equal(before, File.ReadAllText(Path.Combine(project.Home, ProjectStore.ManifestFileName)));
    }

    [Fact]
    public async Task PrepareSubmission_RejectsAnOversizedJournalBeforePublication()
    {
        var project = _store.Create(new string('g', 100_000), null, null);
        var manifestPath = Path.Combine(project.Home, ProjectStore.ManifestFileName);
        var before = File.ReadAllText(manifestPath);

        var failure = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            _store.PrepareSubmissionAsync(project.Home, Guid.NewGuid(), null, "small input", CancellationToken.None));

        Assert.Equal(ProjectOperationErrorCode.InvalidMissionInput, failure.Code);
        Assert.Equal(before, File.ReadAllText(manifestPath));
    }

    [Fact]
    public async Task AFailedReceiptPublication_LeavesPreparedIntentAndReportsSubmissionUncertain()
    {
        var project = _store.Create("Ship a release", null, null);
        var commandId = Guid.NewGuid();
        await _store.PrepareSubmissionAsync(project.Home, commandId, null, "first", CancellationToken.None);
        var temporaryId = Guid.NewGuid();
        Directory.CreateDirectory(Path.Combine(project.Home, $".forge-project.{temporaryId:N}.tmp"));
        var failingStore = new ProjectStore(ProjectsRoot, new ProjectManifestFile(() => temporaryId));

        var failure = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            failingStore.RecordSubmissionAcceptedAsync(
                project.Home,
                commandId,
                new ProjectSubmissionAcceptance(Guid.NewGuid(), Guid.NewGuid(), 1, ConversationRunStatus.Queued),
                CancellationToken.None));

        Assert.Equal(ProjectOperationErrorCode.SubmissionUncertain, failure.Code);
        Assert.Equal(ProjectSubmissionPhase.Prepared, _store.Open(project.Home).Project!.Manifest.Submission!.Phase);
    }

    [Fact]
    public async Task ConcurrentSelectionAndContainerWrites_PreserveBothValues()
    {
        var project = _store.Create("Ship a release", null, null);
        var secondStore = new ProjectStore(ProjectsRoot);
        var containerId = Guid.NewGuid();

        await Task.WhenAll(
            _store.SelectMissionAsync(project.Home, "Naive", CancellationToken.None),
            secondStore.SetProjectMissionContainerIdAsync(project.Home, containerId, CancellationToken.None));

        var manifest = _store.Open(project.Home).Project!.Manifest;
        Assert.Equal("Naive", manifest.SelectedMission.Reference);
        Assert.Equal(containerId, manifest.ProjectMissionContainerId);
        Assert.Empty(Directory.GetFiles(project.Home, "*.tmp"));
    }

    [Fact]
    public async Task AHeldProjectLease_ReturnsProjectBusy_WithoutChangingTheManifest()
    {
        var project = _store.Create("Ship a release", null, null);
        var manifestPath = Path.Combine(project.Home, ProjectStore.ManifestFileName);
        var before = File.ReadAllText(manifestPath);
        await using var heldLease = new FileStream(
            Path.Combine(project.Home, ProjectManifestFile.LockFileName),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);

        var failure = await Assert.ThrowsAsync<ProjectOperationException>(() =>
            _store.SelectMissionAsync(project.Home, "Naive", CancellationToken.None));

        Assert.Equal(ProjectOperationErrorCode.ProjectBusy, failure.Code);
        Assert.Equal(before, File.ReadAllText(manifestPath));
    }

    [Fact]
    public async Task WaitingForAProjectLease_HonoursCancellationWithoutChangingTheManifest()
    {
        var project = _store.Create("Ship a release", null, null);
        var manifestPath = Path.Combine(project.Home, ProjectStore.ManifestFileName);
        var before = File.ReadAllText(manifestPath);
        await using var heldLease = new FileStream(
            Path.Combine(project.Home, ProjectManifestFile.LockFileName),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _store.SelectMissionAsync(project.Home, "Naive", cancellation.Token));

        Assert.Equal(before, File.ReadAllText(manifestPath));
    }

    [Fact]
    public async Task SeparateProcesses_UpdateSixtyProjectsWithoutLosingEitherValue()
    {
        const int projectCount = 60;
        var homes = new List<string>(projectCount);
        for (var index = 0; index < projectCount; index++)
        {
            var project = _store.Create($"Process {index}", null, null);
            await _store.PrepareSubmissionAsync(project.Home, Guid.NewGuid(), null, "record receipt", CancellationToken.None);
            homes.Add(project.Home);
        }

        var containerId = Guid.NewGuid();
        var receipt = StartProcessWorker("receipt", containerId, projectCount);
        var container = StartProcessWorker("container", containerId, projectCount);
        await Task.WhenAll(WaitForExitAsync(receipt), WaitForExitAsync(container));

        foreach (var home in homes)
        {
            var manifest = _store.Open(home).Project!.Manifest;
            Assert.Equal(containerId, manifest.ProjectMissionContainerId);
            Assert.Equal(ProjectSubmissionPhase.Accepted, manifest.Submission!.Phase);
            Assert.Equal(containerId, manifest.Submission.Acceptance!.ContainerId);
            Assert.Empty(Directory.GetFiles(home, "*.tmp"));
        }
    }

    [Theory]
    [InlineData("BeforeFlush")]
    [InlineData("BeforeRename")]
    [InlineData("AfterRename")]
    public void PublicationFaults_LeaveEitherTheOldOrNewValidManifest_AndOnlyCleanTheirOwnTemporaryFile(
        string boundaryName)
    {
        var boundary = Enum.Parse<ProjectManifestPublicationBoundary>(boundaryName);
        var project = _store.Create("Ship a release", null, null);
        var containerId = Guid.NewGuid();
        var temporaryId = Guid.NewGuid();
        var ownTemporaryPath = Path.Combine(project.Home, $".forge-project.{temporaryId:N}.tmp");
        var otherTemporaryPath = Path.Combine(project.Home, $".forge-project.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(otherTemporaryPath, "another operation's stale temp");
        var faultingStore = new ProjectStore(
            ProjectsRoot,
            new ProjectManifestFile(
                () => temporaryId,
                reached =>
                {
                    if (reached == boundary)
                        throw new IOException($"Injected {boundary} publication fault.");
                }));

        var failure = Assert.Throws<ProjectOperationException>(
            () => faultingStore.SetProjectMissionContainerId(project.Home, containerId));

        Assert.Equal(ProjectOperationErrorCode.ManifestWriteFailed, failure.Code);
        var reread = _store.Open(project.Home).Project!.Manifest;
        if (boundary == ProjectManifestPublicationBoundary.AfterRename)
            Assert.Equal(containerId, reread.ProjectMissionContainerId);
        else
            Assert.Null(reread.ProjectMissionContainerId);
        Assert.False(File.Exists(ownTemporaryPath));
        Assert.True(File.Exists(otherTemporaryPath));
    }

    [Theory]
    [InlineData("crash-before-flush", false, true)]
    [InlineData("crash-before-rename", false, true)]
    [InlineData("crash-after-rename", true, false)]
    public async Task CrashAtPublicationBoundary_LeavesAnOldOrNewValidManifest_AndNeverPromotesStaleTemps(
        string operation,
        bool writesNewManifest,
        bool leavesStaleTemporaryFile)
    {
        var project = _store.Create("Process 0", null, null);
        var containerId = Guid.NewGuid();
        var crashing = StartProcessWorker(operation, containerId, projectCount: 1);

        await WaitForCrashAsync(crashing);

        var afterCrash = _store.Open(project.Home).Project!.Manifest;
        Assert.Equal(writesNewManifest ? containerId : null, afterCrash.ProjectMissionContainerId);
        var staleFiles = Directory.GetFiles(project.Home, ".forge-project.*.tmp");
        Assert.Equal(leavesStaleTemporaryFile, staleFiles.Length == 1);

        // Reopening and a later unrelated mutation read only forge.project.json. A crash-left
        // temporary file stays ignored rather than being promoted or cleaned by another operation.
        await _store.SelectMissionAsync(project.Home, "Naive", CancellationToken.None);
        var afterRecovery = _store.Open(project.Home).Project!.Manifest;
        Assert.Equal("Naive", afterRecovery.SelectedMission.Reference);
        Assert.Equal(writesNewManifest ? containerId : null, afterRecovery.ProjectMissionContainerId);
        Assert.Equal(leavesStaleTemporaryFile, Directory.GetFiles(project.Home, ".forge-project.*.tmp").Length == 1);
    }

    private Process StartProcessWorker(string operation, Guid containerId, int projectCount)
    {
        var configuration = Directory.GetParent(AppContext.BaseDirectory)!.Parent!.Name;
        var probeAssembly = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "ForgeMission.ProjectStoreProbe", "bin", configuration, "net10.0",
            "ForgeMission.ProjectStoreProbe.dll"));
        Assert.True(File.Exists(probeAssembly), $"The ProjectStore process probe was not built at {probeAssembly}.");
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(probeAssembly)!,
        };
        start.ArgumentList.Add(probeAssembly);
        start.ArgumentList.Add(ProjectsRoot);
        start.ArgumentList.Add(operation);
        start.ArgumentList.Add(containerId.ToString());
        start.ArgumentList.Add(projectCount.ToString());
        return Process.Start(start) ?? throw new InvalidOperationException("Could not start the process probe.");
    }

    private static async Task WaitForExitAsync(Process process)
    {
        await process.WaitForExitAsync();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        Assert.True(process.ExitCode == 0, $"Process probe failed:{Environment.NewLine}{output}{error}");
    }

    private static async Task WaitForCrashAsync(Process process)
    {
        await process.WaitForExitAsync();
        Assert.NotEqual(0, process.ExitCode);
    }

    // --- v1 completeness ----------------------------------------------------------------------

    // Tasks 3/4 add facts to these collections. Proving the full graph round-trips now is what
    // makes that an append rather than a silent schema change — and Task 2's conversation-ID
    // write-back depends on nothing being dropped on the way through.
    [Fact]
    public async Task AFullyPopulatedV1Manifest_NextSuccessfulMutationWritesV3WithoutDroppingData()
    {
        var home = WriteManifest(FullyPopulatedManifestJson);

        await _store.SelectMissionAsync(home, "Naive", CancellationToken.None);
        var manifestPath = Path.Combine(home, ProjectStore.ManifestFileName);
        var rewritten = File.ReadAllText(manifestPath);
        var reread = _store.Open(home).Project!.Manifest;

        Assert.Equal(3, reread.SchemaVersion);
        Assert.Equal(Guid.Parse("9f000000-0000-0000-0000-0000000000aa"), reread.LegacyProjectControlConversationId);
        Assert.Equal(ProjectAssetKind.LockFile, reread.Assets[1].Kind);
        Assert.Equal("mission/mission.mcl", reread.Assets[0].RelativePath);
        Assert.Equal("sha256:asset", reread.Assets[0].ContentHash);
        Assert.Equal(ProjectContextKind.SourceRoot, reread.AttachedContext[0].Kind);
        Assert.Equal(ProjectContextKind.Artifact, reread.AttachedContext[1].Kind);
        Assert.Equal("artifact-77", reread.AttachedContext[1].Reference);

        var run = Assert.Single(reread.Runs);
        Assert.Equal(ConversationRunStatus.Completed, run.Status);
        Assert.Equal(Guid.Parse("9f000000-0000-0000-0000-0000000000cc"), run.PredecessorRunId);
        Assert.Equal(ProjectMissionOrigin.Oci, run.LaunchSnapshot.Mission.Origin);
        Assert.Equal("sha256:bundle", run.LaunchSnapshot.Mission.Digest);
        Assert.Equal("sha256:mission", run.LaunchSnapshot.LocalMissionContentHash);
        Assert.Equal("sha256:expert", Assert.Single(run.LaunchSnapshot.ResolvedExperts).Digest);
        Assert.Equal("c1", Assert.Single(run.LaunchSnapshot.Context).ContextId);
        Assert.Equal("artifact-77", Assert.Single(run.LaunchSnapshot.Artifacts).ArtifactId);
        Assert.Equal("abc1234", run.LaunchSnapshot.GitRevision);
        Assert.Equal("Naive", reread.SelectedMission.Reference);
        // The durable status keeps the wire spelling ConversationHost produces, not a local one.
        Assert.Contains("\"status\": \"completed\"", rewritten, StringComparison.Ordinal);
    }

    private const string FullyPopulatedManifestJson = """
        {
          "schemaVersion": 1,
          "projectId": "9f000000-0000-0000-0000-0000000000bb",
          "title": "Todos API",
          "goal": "Build a todos API",
          "assets": [
            { "kind": "Mission", "relativePath": "mission/mission.mcl", "contentHash": "sha256:asset" },
            { "kind": "LockFile", "relativePath": "mission/mcl.lock", "contentHash": null }
          ],
          "selectedMission": { "origin": "Local", "reference": "mission/mission.mcl", "digest": null },
          "attachedContext": [
            { "id": "c1", "kind": "SourceRoot", "displayName": "api", "reference": "/src/api", "contentHash": null },
            { "id": "c2", "kind": "Artifact", "displayName": "spec", "reference": "artifact-77", "contentHash": "sha256:spec" }
          ],
          "missionControlConversationId": "9f000000-0000-0000-0000-0000000000aa",
          "runs": [
            {
              "runId": "9f000000-0000-0000-0000-0000000000dd",
              "title": "Add pagination",
              "status": "completed",
              "predecessorRunId": "9f000000-0000-0000-0000-0000000000cc",
              "launchSnapshot": {
                "mission": { "origin": "Oci", "reference": "ghcr.io/katasec/janus", "digest": "sha256:bundle" },
                "localMissionContentHash": "sha256:mission",
                "resolvedExperts": [ { "reference": "ghcr.io/katasec/proposer", "digest": "sha256:expert" } ],
                "context": [ { "contextId": "c1", "contentHash": null } ],
                "gitRevision": "abc1234",
                "artifacts": [ { "artifactId": "artifact-77", "contentHash": null } ]
              }
            }
          ]
        }
        """;

    private static string FullyPopulatedV2ManifestJson(Guid containerId, Guid legacyId) =>
        FullyPopulatedManifestJson
            .Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal)
            .Replace(
                "\"missionControlConversationId\": \"9f000000-0000-0000-0000-0000000000aa\"",
                $"\"projectMissionContainerId\": \"{containerId}\", \"legacyProjectControlConversationId\": \"{legacyId}\"",
                StringComparison.Ordinal);

    private string WriteManifest(string json)
    {
        var home = Directory.CreateDirectory(Path.Combine(_profile, "manifests", Guid.NewGuid().ToString("N"))).FullName;
        File.WriteAllText(Path.Combine(home, ProjectStore.ManifestFileName), json);
        return home;
    }

    private void AssertRefused(string home, ProjectOperationErrorCode expected)
    {
        var manifestPath = Path.Combine(home, ProjectStore.ManifestFileName);
        var before = File.ReadAllText(manifestPath);

        var failure = Assert.Throws<ProjectOperationException>(() => _store.Open(home));

        Assert.Equal(expected, failure.Code);
        Assert.Contains(manifestPath, failure.Message, StringComparison.Ordinal);
        // A refused manifest is the user's data, never a corrupt cache to overwrite or repair.
        Assert.Equal(before, File.ReadAllText(manifestPath));
    }

    // --- Mission Control conversation id write-back (43.20 task 2) --------------------------

    [Fact]
    public void SetMissionControlConversationId_RecordsIt_AndRoundTripsThroughOpen()
    {
        var project = _store.Create("Todos API", null, null);
        Assert.Null(project.Manifest.LegacyProjectControlConversationId);
        var conversationId = Guid.NewGuid();

        var updated = _store.SetMissionControlConversationId(project.Home, conversationId);

        Assert.Equal(conversationId, updated.Manifest.LegacyProjectControlConversationId);
        Assert.Equal(conversationId, _store.Open(project.Home).Project!.Manifest.LegacyProjectControlConversationId);
        // The rest of the v1 manifest survives the rewrite unchanged.
        Assert.Equal(project.Manifest.ProjectId, updated.Manifest.ProjectId);
        Assert.Equal(project.Manifest.Goal, updated.Manifest.Goal);
        Assert.Equal(ProjectManifest.CurrentSchemaVersion, updated.Manifest.SchemaVersion);
        // No temporary file is left behind.
        Assert.Empty(Directory.GetFiles(project.Home, "*.tmp"));
    }

    [Fact]
    public void SetMissionControlConversationId_WithTheSameId_IsAnIdempotentNoOp()
    {
        var project = _store.Create("Todos API", null, null);
        var conversationId = Guid.NewGuid();
        _store.SetMissionControlConversationId(project.Home, conversationId);
        var manifestPath = Path.Combine(project.Home, ProjectStore.ManifestFileName);
        var after = File.ReadAllText(manifestPath);

        var again = _store.SetMissionControlConversationId(project.Home, conversationId);

        Assert.Equal(conversationId, again.Manifest.LegacyProjectControlConversationId);
        Assert.Equal(after, File.ReadAllText(manifestPath));
    }

    // A Project has exactly one control conversation. Repointing it would orphan a durable
    // transcript, so a different non-null id is refused rather than overwritten.
    [Fact]
    public void SetMissionControlConversationId_WithADifferentId_IsRefused_AndLeavesTheFileUntouched()
    {
        var project = _store.Create("Todos API", null, null);
        _store.SetMissionControlConversationId(project.Home, Guid.NewGuid());
        var manifestPath = Path.Combine(project.Home, ProjectStore.ManifestFileName);
        var before = File.ReadAllText(manifestPath);

        var failure = Assert.Throws<ProjectOperationException>(
            () => _store.SetMissionControlConversationId(project.Home, Guid.NewGuid()));

        Assert.Equal(ProjectOperationErrorCode.MissionRunConflict, failure.Code);
        Assert.Equal(before, File.ReadAllText(manifestPath));
    }

    [Fact]
    public void SetMissionControlConversationId_WithNoManifest_ReportsHomeNotFound()
    {
        var empty = Path.Combine(_profile, "no-project");
        Directory.CreateDirectory(empty);

        var failure = Assert.Throws<ProjectOperationException>(
            () => _store.SetMissionControlConversationId(empty, Guid.NewGuid()));

        Assert.Equal(ProjectOperationErrorCode.HomeNotFound, failure.Code);
    }

    // A failed replacement is named ManifestWriteFailed and leaves the original manifest intact —
    // the durable conversation stays valid, and the retry re-derives the same id.
    [Fact]
    public void AFailedReplacement_ReportsManifestWriteFailed_AndLeavesTheOriginalManifestIntact()
    {
        var project = _store.Create("Todos API", null, null);
        var manifestPath = Path.Combine(project.Home, ProjectStore.ManifestFileName);
        var before = File.ReadAllText(manifestPath);

        // A directory where the temporary file must be written makes the write fail without
        // touching the manifest itself.
        var temporaryId = Guid.NewGuid();
        var temporaryPath = Path.Combine(project.Home, $".forge-project.{temporaryId:N}.tmp");
        Directory.CreateDirectory(temporaryPath);

        var failingStore = new ProjectStore(ProjectsRoot, new ProjectManifestFile(() => temporaryId));

        var failure = Assert.Throws<ProjectOperationException>(
            () => failingStore.SetMissionControlConversationId(project.Home, Guid.NewGuid()));

        Assert.Equal(ProjectOperationErrorCode.ManifestWriteFailed, failure.Code);
        Assert.Equal(before, File.ReadAllText(manifestPath));
        Assert.Null(_store.Open(project.Home).Project!.Manifest.LegacyProjectControlConversationId);
    }
}
