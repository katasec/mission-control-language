using System.Text.Json;
using ForgeMission.Application;
using ForgeMission.Application.Transport;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Tests.Application;

/// <summary>
/// Phase 48. The managed chat Project is the one piece of local state this journey creates, so these
/// tests hold its boundary: it is provisioned once with its shipped Janus version, reused unchanged
/// afterwards, repaired only through the marker, and never overwritten when it is a real Project that
/// has gone wrong.
/// </summary>
public sealed class ManagedChatProjectServiceTests : IDisposable
{
    private readonly string profile = Path.Combine(Path.GetTempPath(), "forge-managed-chat", Guid.NewGuid().ToString("N"));
    private readonly string root;
    private readonly ProjectService projects;
    private readonly MissionVersionService versions;
    private readonly ManagedChatProjectService managed;

    public ManagedChatProjectServiceTests()
    {
        root = Path.Combine(profile, "Forge", "Projects");
        projects = new ProjectService(root);
        versions = new MissionVersionService(projects);
        managed = new ManagedChatProjectService(projects, versions);
    }

    [Fact]
    public async Task FirstResolve_ProvisionsOneGuidKeyedProject_WithTheShippedApprovedVersion()
    {
        var resolution = await managed.ResolveAsync(CancellationToken.None);

        Assert.Equal(ManagedChatProjectOutcome.Provisioned, resolution.Outcome);
        var record = projects.ReadForHome(resolution.Home!);
        Assert.Equal(Path.Combine(root, record.Manifest.ProjectId.ToString("D")), resolution.Home);
        Assert.Equal(record.Manifest.ProjectId, Marker());
        // A friendly two-word title, generated once, is ordinary Project display data.
        Assert.Equal(2, record.Manifest.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);

        var definition = Assert.Single(record.Manifest.MissionDefinitions!);
        var version = Assert.Single(definition.Versions!);
        Assert.Equal("Janus", definition.Name);
        Assert.Equal(resolution.MissionId, definition.MissionId);
        Assert.Equal(version.MissionVersionId, definition.ActiveApprovedVersionId);
        Assert.Equal(MissionVersionState.Approved, version.State);
        Assert.Equal(1, version.VersionNumber);
        Assert.Equal("1.4", version.ReleaseLabel);
        Assert.Equal(MissionHandsProfile.ProjectWorkspace, version.CapabilityProfile);
        // Release-reviewed code records no outcome Forge did not observe.
        Assert.Empty(version.EvaluationCases!);
        Assert.Empty(version.EvaluationResults!);
        Assert.Equal(["Proposer", "Approver"], version.Package.ResolvedExperts.Select(expert => expert.Name));
    }

    [Fact]
    public async Task TheCoordinator_WritesOnlyTheMarker_AndLeavesEveryProjectFileToProjectService()
    {
        var resolution = await managed.ResolveAsync(CancellationToken.None);

        // Everything else it produced lives inside the home ProjectService created.
        var atRoot = Directory.GetFiles(root).Select(path => Path.GetFileName(path)!).ToArray();
        Assert.Equal([ManagedChatProjectMarkerFile.FileName], atRoot);
        Assert.Equal([resolution.Home!], Directory.GetDirectories(root));
        Assert.True(File.Exists(Path.Combine(resolution.Home!, ProjectService.ManifestFileName)));
        Assert.True(File.Exists(Path.Combine(resolution.Home!, "mcl.lock")));
        Assert.True(File.Exists(Path.Combine(resolution.Home!, "experts", "Proposer", "expert.md")));
        Assert.True(File.Exists(Path.Combine(resolution.Home!, "experts", "Approver", "expert.md")));
    }

    [Fact]
    public async Task SecondResolve_ReusesTheSameProject_AndRewritesNothing()
    {
        var first = await managed.ResolveAsync(CancellationToken.None);
        var manifest = Path.Combine(first.Home!, ProjectService.ManifestFileName);
        var before = File.ReadAllBytes(manifest);
        var stamp = File.GetLastWriteTimeUtc(manifest);

        var second = await managed.ResolveAsync(CancellationToken.None);

        Assert.Equal(ManagedChatProjectOutcome.Reused, second.Outcome);
        Assert.Equal(first.Home, second.Home);
        Assert.Equal(first.MissionId, second.MissionId);
        Assert.Equal(before, File.ReadAllBytes(manifest));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(manifest));
        Assert.Single(Directory.GetDirectories(root));
    }

    [Fact]
    public async Task AMarkerNamingAMissingProject_IsRepairedByProvisioningAFreshOne_LeavingOtherProjectsAlone()
    {
        var unrelated = projects.Create("Someone else's Project", null, null);
        var unrelatedBefore = File.ReadAllBytes(Path.Combine(unrelated.Home, ProjectService.ManifestFileName));
        await new ManagedChatProjectMarkerFile(root).WriteAsync(Guid.NewGuid(), CancellationToken.None);

        var resolution = await managed.ResolveAsync(CancellationToken.None);

        Assert.Equal(ManagedChatProjectOutcome.Provisioned, resolution.Outcome);
        Assert.Equal(projects.ReadForHome(resolution.Home!).Manifest.ProjectId, Marker());
        Assert.Equal(unrelatedBefore, File.ReadAllBytes(Path.Combine(unrelated.Home, ProjectService.ManifestFileName)));
    }

    [Fact]
    public async Task AnUnreadableMarker_IsRepairedByProvisioningAFreshOne()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, ManagedChatProjectMarkerFile.FileName), "{ not json");

        var resolution = await managed.ResolveAsync(CancellationToken.None);

        Assert.Equal(ManagedChatProjectOutcome.Provisioned, resolution.Outcome);
        Assert.Equal(projects.ReadForHome(resolution.Home!).Manifest.ProjectId, Marker());
    }

    [Fact]
    public async Task AMalformedManagedProject_IsReported_AndNeverOverwritten()
    {
        var provisioned = await managed.ResolveAsync(CancellationToken.None);
        var manifest = Path.Combine(provisioned.Home!, ProjectService.ManifestFileName);
        File.WriteAllText(manifest, "{ \"schemaVersion\": 5 }");
        var before = File.ReadAllBytes(manifest);

        var resolution = await managed.ResolveAsync(CancellationToken.None);

        Assert.Equal(ManagedChatProjectOutcome.InvalidProject, resolution.Outcome);
        Assert.NotNull(resolution.Reason);
        Assert.Equal(before, File.ReadAllBytes(manifest));
        Assert.Single(Directory.GetDirectories(root));
    }

    [Fact]
    public async Task AManagedProjectWithoutItsShippedVersion_IsReported_RatherThanRebuiltOverTheTop()
    {
        var provisioned = await managed.ResolveAsync(CancellationToken.None);
        var record = projects.ReadForHome(provisioned.Home!);
        File.WriteAllText(Path.Combine(provisioned.Home!, ProjectService.ManifestFileName),
            JsonSerializer.Serialize(record.Manifest with { MissionDefinitions = [] }, ProjectManifestJsonContext.Default.ProjectManifest));

        var resolution = await managed.ResolveAsync(CancellationToken.None);

        Assert.Equal(ManagedChatProjectOutcome.InvalidProject, resolution.Outcome);
        Assert.Empty(projects.ReadForHome(provisioned.Home!).Manifest.MissionDefinitions!);
    }

    [Fact]
    public async Task TheShippedVersionWriter_RefusesAProjectWhosePackageInputsAreMissing()
    {
        // The one reachable way the shipped package cannot be built: its Project lists the lock file a
        // package is built from, but that file is not there. This is the refusal the coordinator maps to
        // its typed unavailable outcome, so nothing downstream is created from a package that failed.
        var project = projects.Create("Broken package", null, null);
        WriteManifest(project.Home, projects.ReadForHome(project.Home).Manifest with
        {
            Assets = [new ProjectAssetDescriptor(ProjectAssetKind.LockFile, "mcl.lock", null)],
        });

        var refused = await Assert.ThrowsAsync<ProjectOperationException>(() => versions.EnsureShippedApprovedVersionAsync(
            project.Home, "Janus", "mission Janus(task) = {\n    Proposer\n    -> Approver\n}\n",
            MissionHandsProfile.ProjectWorkspace, "1.4", CancellationToken.None));

        Assert.Equal(ProjectOperationErrorCode.InvalidManifest, refused.Code);
        Assert.Empty(projects.ReadForHome(project.Home).Manifest.MissionDefinitions!);
    }

    private static void WriteManifest(string home, ProjectManifest manifest) =>
        File.WriteAllText(Path.Combine(home, ProjectService.ManifestFileName),
            JsonSerializer.Serialize(manifest, ProjectManifestJsonContext.Default.ProjectManifest));

    private Guid Marker()
    {
        var read = new ManagedChatProjectMarkerFile(root).Read();
        Assert.Equal(ManagedChatProjectMarkerState.Found, read.State);
        return read.ProjectId;
    }

    public void Dispose()
    {
        if (Directory.Exists(profile)) Directory.Delete(profile, recursive: true);
    }
}
