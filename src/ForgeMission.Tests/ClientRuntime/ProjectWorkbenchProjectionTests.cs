using System.Text;
using System.Text.Json;
using ForgeMission.ClientRuntime.Services;
using ForgeMission.ClientRuntime.Transport;

namespace ForgeMission.Tests.ClientRuntime;

/// <summary>
/// Phase 43.20 Task 3 — the Explorer projection and document read.
///
/// Two invariants carry most of the weight and are asserted directly rather than inferred:
/// no payload the projector produces contains a local path, and an entry ID is matched against a
/// freshly built projection rather than decoded into a location. Everything else is about
/// refusing to show a Project state that is not actually true — a partial dependency list would
/// read as "these are the dependencies".
///
/// These are in-process unit checks, not acceptance. The positive OCI dependency cases need a
/// redirected user profile so they cannot touch the developer's real expert cache; they live in
/// <see cref="ProjectTransportContractTests"/>, which runs the Runtime as a child process.
/// </summary>
public sealed class ProjectWorkbenchProjectionTests : IDisposable
{
    private readonly string _profile = Directory.CreateTempSubdirectory("forge-workbench-").FullName;
    private readonly ProjectStore _projects;
    private readonly ProjectWorkbenchProjector _workbench;

    public ProjectWorkbenchProjectionTests()
    {
        _projects = new ProjectStore(Path.Combine(_profile, "Forge", "Projects"));
        _workbench = new ProjectWorkbenchProjector(_projects);
    }

    public void Dispose() => Directory.Delete(_profile, recursive: true);

    // --- projection ---------------------------------------------------------------------------

    [Fact]
    public void A_new_project_projects_three_empty_sections_and_no_failure()
    {
        var home = NewProject();

        var projection = _workbench.Project(home);

        Assert.Empty(projection.Assets);
        Assert.Empty(projection.Context);
        Assert.Empty(projection.Runs);
        Assert.Equal("Ship the thing", projection.Project.Goal);
    }

    // An absent mcl.lock is the ordinary state of a Project that has no dependencies, not a fault.
    [Fact]
    public void A_project_with_no_lock_file_reports_no_dependencies_rather_than_failing()
    {
        var home = NewProject();

        Assert.Empty(_workbench.Project(home).Assets);
    }

    [Fact]
    public void Declared_local_assets_are_listed_as_editable_entries()
    {
        var home = NewProject();
        WriteFile(home, "mission.mcl", "mission Demo\n  Answerer\n");
        WriteFile(home, "experts/Answerer/expert.md", ExpertMarkdown);
        Declare(home,
            new ProjectAssetDescriptor(ProjectAssetKind.Mission, "mission.mcl", null),
            new ProjectAssetDescriptor(ProjectAssetKind.Expert, "experts/Answerer/expert.md", null));

        var assets = _workbench.Project(home).Assets;

        Assert.Collection(assets,
            mission =>
            {
                Assert.Equal(ProjectExplorerEntryKind.Mission, mission.Kind);
                Assert.Equal("mission.mcl", mission.DisplayName);
                Assert.False(mission.IsReadOnly);
                Assert.Null(mission.Source);
            },
            expert =>
            {
                Assert.Equal(ProjectExplorerEntryKind.Expert, expert.Kind);
                Assert.False(expert.IsReadOnly);
            });
    }

    // A project-sourced lock entry is a local expert, so it is editable and shows no source: a
    // source line for it would be a local path, which must not cross this boundary.
    [Fact]
    public void A_project_sourced_lock_entry_is_an_editable_local_expert_with_no_source_shown()
    {
        var home = NewProject();
        var expert = WriteFile(home, "experts/Answerer/expert.md", ExpertMarkdown);
        WriteLock(home, $"""
            version: 2
            experts:
              Answerer:
                source: project:///experts/Answerer/expert.md
                contentDigest: {Digest(expert)}
            """);

        var entry = Assert.Single(_workbench.Project(home).Assets);

        Assert.Equal(ProjectExplorerEntryKind.Expert, entry.Kind);
        Assert.False(entry.IsReadOnly);
        Assert.Null(entry.Source);
    }

    [Fact]
    public void Attached_context_is_listed_by_display_name_only()
    {
        var home = NewProject();
        Attach(home, new ProjectContextDescriptor(
            "ctx-1", ProjectContextKind.SourceRoot, "forge-infra", "/Users/someone/progs/forge-infra", null));

        var entry = Assert.Single(_workbench.Project(home).Context);

        Assert.Equal(ProjectExplorerEntryKind.SourceRoot, entry.Kind);
        Assert.Equal("forge-infra", entry.DisplayName);
        // The manifest's reference for a SourceRoot is an ABSOLUTE local path. It stays local.
        Assert.Null(entry.Source);
        Assert.DoesNotContain("/Users/someone", Serialize(entry), StringComparison.Ordinal);
    }

    // The strongest form of the no-path rule: assert it against the serialized payload, so a field
    // added later that happens to carry one fails here rather than shipping.
    [Fact]
    public void No_projected_payload_contains_the_projects_location()
    {
        var home = NewProject();
        WriteFile(home, "mission.mcl", "mission Demo\n");
        Declare(home, new ProjectAssetDescriptor(ProjectAssetKind.Mission, "mission.mcl", null));

        var assets = _workbench.Project(home).Assets;

        Assert.DoesNotContain(home, Serialize(assets), StringComparison.Ordinal);
    }

    // --- refused projections -------------------------------------------------------------------

    [Fact]
    public void A_declared_asset_that_is_not_present_fails_rather_than_shortening_the_list()
    {
        var home = NewProject();
        Declare(home, new ProjectAssetDescriptor(ProjectAssetKind.Mission, "mission.mcl", null));

        var failure = Assert.Throws<ProjectOperationException>(() => _workbench.Project(home));

        Assert.Equal(ProjectOperationErrorCode.InvalidManifest, failure.Code);
    }

    [Fact]
    public void An_unreadable_lock_file_fails_the_whole_projection()
    {
        var home = NewProject();
        WriteLock(home, "this: is: not: a lock file\n  - [");

        Assert.Equal(
            ProjectOperationErrorCode.InvalidDependency,
            Assert.Throws<ProjectOperationException>(() => _workbench.Project(home)).Code);
    }

    [Fact]
    public void A_malformed_source_uri_fails_the_whole_projection()
    {
        var home = NewProject();
        WriteLock(home, $"""
            version: 2
            experts:
              Answerer:
                source: not-a-source
                contentDigest: sha256:{new string('a', 64)}
            """);

        Assert.Equal(
            ProjectOperationErrorCode.InvalidDependency,
            Assert.Throws<ProjectOperationException>(() => _workbench.Project(home)).Code);
    }

    [Fact]
    public void A_recorded_expert_that_is_not_present_fails_the_whole_projection()
    {
        var home = NewProject();
        WriteLock(home, $"""
            version: 2
            experts:
              Answerer:
                source: project:///experts/Answerer/expert.md
                contentDigest: sha256:{new string('a', 64)}
            """);

        Assert.Equal(
            ProjectOperationErrorCode.InvalidDependency,
            Assert.Throws<ProjectOperationException>(() => _workbench.Project(home)).Code);
    }

    // Pinned evidence that no longer describes the file is worse than no evidence.
    [Fact]
    public void A_content_digest_that_no_longer_matches_fails_the_whole_projection()
    {
        var home = NewProject();
        WriteFile(home, "experts/Answerer/expert.md", ExpertMarkdown);
        WriteLock(home, $"""
            version: 2
            experts:
              Answerer:
                source: project:///experts/Answerer/expert.md
                contentDigest: sha256:{new string('a', 64)}
            """);

        var failure = Assert.Throws<ProjectOperationException>(() => _workbench.Project(home));

        Assert.Equal(ProjectOperationErrorCode.InvalidDependency, failure.Code);
        Assert.Contains("Answerer", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_v1_oci_lock_file_fails_with_the_runtimes_named_dependency_failure()
    {
        var home = NewProject();
        WriteLock(home, """
            version: 1
            experts:
              KubernetesArchitect:
                source: ghcr.io/katasec/forge-kubernetes-architect:0.1.0
                path: ~/.forge/experts/ghcr.io/katasec/forge-kubernetes-architect/0.1.0/expert.md
            """);

        var failure = Assert.Throws<ProjectOperationException>(() => _workbench.Project(home));

        Assert.Equal(ProjectOperationErrorCode.InvalidDependency, failure.Code);
        Assert.Contains("forge init", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_home_is_a_named_failure_rather_than_an_empty_projection()
    {
        Assert.Equal(
            ProjectOperationErrorCode.HomeNotFound,
            Assert.Throws<ProjectOperationException>(
                () => _workbench.Project(Path.Combine(_profile, "nowhere"))).Code);
    }

    // --- documents ------------------------------------------------------------------------------

    [Fact]
    public void An_allowed_entry_opens_as_utf8_text()
    {
        var home = NewProject();
        WriteFile(home, "mission.mcl", "mission Demo\n  Answerer\n");
        Declare(home, new ProjectAssetDescriptor(ProjectAssetKind.Mission, "mission.mcl", null));
        var entry = Assert.Single(_workbench.Project(home).Assets);

        var document = _workbench.OpenDocument(home, entry.EntryId);

        Assert.Equal("mission.mcl", document.Title);
        Assert.Equal("text/plain", document.ContentType);
        Assert.Equal("mission Demo\n  Answerer\n", document.Text);
        Assert.DoesNotContain(home, Serialize(document), StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_entry_id_is_not_found()
    {
        var home = NewProject();

        Assert.Equal(
            ProjectOperationErrorCode.DocumentNotFound,
            Assert.Throws<ProjectOperationException>(() => _workbench.OpenDocument(home, "asset:mission.mcl")).Code);
    }

    // The ID is matched, never decoded: a forged one that looks like a path resolves to nothing.
    [Theory]
    [InlineData("asset:../../../etc/passwd")]
    [InlineData("asset:/etc/passwd")]
    [InlineData("dep:../../secrets")]
    [InlineData("")]
    public void A_forged_entry_id_resolves_to_nothing(string entryId)
    {
        var home = NewProject();
        WriteFile(home, "mission.mcl", "mission Demo\n");
        Declare(home, new ProjectAssetDescriptor(ProjectAssetKind.Mission, "mission.mcl", null));

        Assert.Equal(
            ProjectOperationErrorCode.DocumentNotFound,
            Assert.Throws<ProjectOperationException>(() => _workbench.OpenDocument(home, entryId)).Code);
    }

    // A stale entry is simply one a fresh projection no longer contains — there is no server-side
    // entry cache that could keep it alive.
    [Fact]
    public void An_entry_removed_since_it_was_listed_is_not_found()
    {
        var home = NewProject();
        WriteFile(home, "mission.mcl", "mission Demo\n");
        Declare(home, new ProjectAssetDescriptor(ProjectAssetKind.Mission, "mission.mcl", null));
        var entry = Assert.Single(_workbench.Project(home).Assets);

        Declare(home); // the Project no longer declares it

        Assert.Equal(
            ProjectOperationErrorCode.DocumentNotFound,
            Assert.Throws<ProjectOperationException>(() => _workbench.OpenDocument(home, entry.EntryId)).Code);
    }

    [Fact]
    public void A_listed_run_has_no_document_surface()
    {
        var home = NewProject();
        AddRun(home, "Draft the release plan");
        var run = Assert.Single(_workbench.Project(home).Runs);

        Assert.Equal(ProjectExplorerEntryKind.Run, run.Kind);
        Assert.Equal(
            ProjectOperationErrorCode.DocumentNotFound,
            Assert.Throws<ProjectOperationException>(() => _workbench.OpenDocument(home, run.EntryId)).Code);
    }

    [Fact]
    public void A_listed_context_root_has_no_document_surface()
    {
        var home = NewProject();
        Attach(home, new ProjectContextDescriptor(
            "ctx-1", ProjectContextKind.SourceRoot, "forge-infra", "/Users/someone/progs/forge-infra", null));
        var context = Assert.Single(_workbench.Project(home).Context);

        Assert.Equal(
            ProjectOperationErrorCode.DocumentNotFound,
            Assert.Throws<ProjectOperationException>(() => _workbench.OpenDocument(home, context.EntryId)).Code);
    }

    [Theory]
    [InlineData(new byte[] { 0x4d, 0x5a, 0x00, 0x01 })]                   // a NUL byte: binary
    [InlineData(new byte[] { 0xff, 0xfe, 0x41, 0x42 })]                   // not valid UTF-8
    public void Content_that_is_not_text_is_refused(byte[] bytes)
    {
        var home = NewProject();
        WriteBytes(home, "mission.mcl", bytes);
        Declare(home, new ProjectAssetDescriptor(ProjectAssetKind.Mission, "mission.mcl", null));
        var entry = Assert.Single(_workbench.Project(home).Assets);

        Assert.Equal(
            ProjectOperationErrorCode.InvalidDocument,
            Assert.Throws<ProjectOperationException>(() => _workbench.OpenDocument(home, entry.EntryId)).Code);
    }

    [Fact]
    public void Content_larger_than_a_mebibyte_is_refused()
    {
        var home = NewProject();
        WriteBytes(home, "mission.mcl", Encoding.UTF8.GetBytes(new string('a', (1024 * 1024) + 1)));
        Declare(home, new ProjectAssetDescriptor(ProjectAssetKind.Mission, "mission.mcl", null));
        var entry = Assert.Single(_workbench.Project(home).Assets);

        Assert.Equal(
            ProjectOperationErrorCode.InvalidDocument,
            Assert.Throws<ProjectOperationException>(() => _workbench.OpenDocument(home, entry.EntryId)).Code);
    }

    [Fact]
    public void Content_at_exactly_a_mebibyte_still_opens()
    {
        var home = NewProject();
        WriteBytes(home, "mission.mcl", Encoding.UTF8.GetBytes(new string('a', 1024 * 1024)));
        Declare(home, new ProjectAssetDescriptor(ProjectAssetKind.Mission, "mission.mcl", null));
        var entry = Assert.Single(_workbench.Project(home).Assets);

        Assert.Equal(1024 * 1024, _workbench.OpenDocument(home, entry.EntryId).Text.Length);
    }

    // --- fixture helpers --------------------------------------------------------------------------

    private const string ExpertMarkdown = """
        ---
        name: Answerer
        input: A question
        output: An answer
        ---

        You answer questions.
        """;

    private string NewProject() => _projects.Create("Ship the thing", null, null).Home;

    private static string Digest(string path) =>
        ForgeMission.Core.Resolution.LockFileIO.ComputeContentDigest(path);

    private static string WriteFile(string home, string relativePath, string content)
    {
        var path = Path.Combine(home, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static void WriteBytes(string home, string relativePath, byte[] bytes)
    {
        var path = Path.Combine(home, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private static void WriteLock(string home, string yaml) => WriteFile(home, "mcl.lock", yaml);

    private void Declare(string home, params ProjectAssetDescriptor[] assets) =>
        Patch(home, manifest => manifest with { Assets = assets });

    private void Attach(string home, params ProjectContextDescriptor[] context) =>
        Patch(home, manifest => manifest with { AttachedContext = context });

    private void AddRun(string home, string title) =>
        Patch(home, manifest => manifest with
        {
            Runs =
            [
                new ProjectRunMetadata(Guid.NewGuid(), title,
                    ForgeMission.Conversations.Contracts.ConversationRunStatus.Completed, null,
                    new ProjectLaunchSnapshot(ProjectMissionReference.BuiltInJanus, null, [], [], null, [])),
            ],
        });

    // The manifest is rewritten directly because Task 1's store deliberately exposes no way to add
    // an asset, a context, or a run yet — those are later tasks. This is a fixture, not a code path.
    private static void Patch(string home, Func<ProjectManifest, ProjectManifest> change)
    {
        var path = Path.Combine(home, ProjectStore.ManifestFileName);
        var manifest = JsonSerializer.Deserialize(
            File.ReadAllText(path), ProjectManifestJsonContext.Default.ProjectManifest)!;
        File.WriteAllText(path, JsonSerializer.Serialize(
            change(manifest), ProjectManifestJsonContext.Default.ProjectManifest));
    }

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, new JsonSerializerOptions { TypeInfoResolver = ClientRuntimeJsonContext.Default });
}
