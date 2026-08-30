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
    public void Create_WritesTheCompleteV1Manifest()
    {
        var created = _store.Create("Todos API", null, null);

        var manifest = created.Manifest;
        Assert.Equal(1, manifest.SchemaVersion);
        Assert.NotEqual(Guid.Empty, manifest.ProjectId);
        Assert.Equal("Todos API", manifest.Title);
        Assert.Equal("Todos API", manifest.Goal);
        Assert.Empty(manifest.Assets);
        Assert.Empty(manifest.AttachedContext);
        Assert.Empty(manifest.Runs);
        Assert.Null(manifest.MissionControlConversationId);
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

        Assert.Contains("\"schemaVersion\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"missionControlConversationId\": null", json, StringComparison.Ordinal);
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
            { "schemaVersion": 2, "projectId": "f0a1b2c3-0000-0000-0000-000000000001",
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

    // --- v1 completeness ----------------------------------------------------------------------

    // Tasks 3/4 add facts to these collections. Proving the full graph round-trips now is what
    // makes that an append rather than a silent schema change — and Task 2's conversation-ID
    // write-back depends on nothing being dropped on the way through.
    [Fact]
    public void AFullyPopulatedV1Manifest_RoundTripsLosslessly()
    {
        var home = WriteManifest(FullyPopulatedManifestJson);

        var manifest = _store.Open(home).Project!.Manifest;
        var rewritten = JsonSerializer.Serialize(manifest, ProjectManifestJsonContext.Default.ProjectManifest);
        var reread = JsonSerializer.Deserialize(rewritten, ProjectManifestJsonContext.Default.ProjectManifest)!;

        Assert.Equal(Guid.Parse("9f000000-0000-0000-0000-0000000000aa"), reread.MissionControlConversationId);
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
}
