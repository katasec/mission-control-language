using ForgeMission.Application.Transport;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Core.Runtime;

namespace ForgeMission.Application;

/// <summary>Why a managed chat Project could not be resolved, or how it was.</summary>
internal enum ManagedChatProjectOutcome
{
    /// <summary>A fresh managed Project was created, with its shipped Janus version.</summary>
    Provisioned,

    /// <summary>The marker named a valid managed Project, which was reopened unchanged.</summary>
    Reused,

    /// <summary>The marker names a Project whose manifest is malformed, or whose shipped mission is
    /// no longer resolvable. It is reported, never repaired by overwriting.</summary>
    InvalidProject,

    /// <summary>The shipped Janus package could not be built or validated. Nothing downstream is
    /// created.</summary>
    PackageUnavailable,
}

/// <summary>What the caller needs and nothing more: no manifest, launch, package, or asset detail.</summary>
internal sealed record ManagedChatProjectResolution(
    ManagedChatProjectOutcome Outcome, string? Home, Guid MissionId, string? Reason);

/// <summary>
/// Resolves the one managed chat Project (Phase 48). It is a coordinator: it reads and writes the
/// marker beside the Projects root, and for everything else it calls <see cref="ProjectService"/>
/// and <see cref="MissionVersionService"/>. It creates no directory, writes no asset, and mutates
/// no manifest itself, because ProjectService remains the sole writer of those.
/// </summary>
internal sealed class ManagedChatProjectService(ProjectService projects, MissionVersionService versions)
{
    private readonly ManagedChatProjectMarkerFile _marker = new(projects.ProjectsRoot);

    /// <summary>Finds the managed Project, or provisions it once. A missing or unreadable marker is
    /// repaired only by creating a fresh managed Project; a marker that names a malformed Project is
    /// reported instead, so a valid Project is never overwritten.</summary>
    internal async Task<ManagedChatProjectResolution> ResolveAsync(CancellationToken ct)
    {
        var marker = _marker.Read();
        if (marker.State != ManagedChatProjectMarkerState.Found)
            return await ProvisionAsync(ct);

        var home = HomeFor(marker.ProjectId);
        if (!Directory.Exists(home) || !File.Exists(Path.Combine(home, ProjectService.ManifestFileName)))
            return await ProvisionAsync(ct);

        try
        {
            var record = projects.ReadForHome(home);
            if (record.Manifest.ProjectId != marker.ProjectId)
                return Invalid(home, "The managed chat Project's manifest names a different Project.");

            var shipped = FindShippedMissionId(record);
            if (shipped is not { } missionId)
                return Invalid(home, "The managed chat Project no longer holds its shipped Janus version.");

            // Resolving the launch is the same check an admission performs, so an unusable package is
            // found here rather than at the moment a chat is created.
            var launch = await versions.ResolveActiveApprovedLaunchAsync(home, missionId, ct);
            return Unusable(launch.Launch)
                ? new ManagedChatProjectResolution(ManagedChatProjectOutcome.PackageUnavailable, home, missionId,
                    "The shipped Janus package is not currently valid.")
                : new ManagedChatProjectResolution(ManagedChatProjectOutcome.Reused, home, missionId, null);
        }
        catch (ProjectOperationException exception)
        {
            return Invalid(home, exception.Message);
        }
    }

    private async Task<ManagedChatProjectResolution> ProvisionAsync(CancellationToken ct)
    {
        var projectId = Guid.NewGuid();
        try
        {
            var created = projects.CreateManaged(projectId, ManagedChatProjectNames.NextTitle(), ShippedMissionCatalog.ProjectGoal);
            await projects.EnsureManagedChatAssetsAsync(created.Home, ct);
            var definition = await versions.EnsureShippedApprovedVersionAsync(created.Home, ShippedMissionCatalog.MissionName,
                ShippedMissionCatalog.Definition, ShippedMissionCatalog.Profile, ShippedMissionCatalog.ReleaseLabel, ct);

            // The marker is written last, so a crash anywhere above leaves no marker claiming a
            // half-provisioned Project.
            await _marker.WriteAsync(projectId, ct);
            return new ManagedChatProjectResolution(ManagedChatProjectOutcome.Provisioned, created.Home, definition.MissionId, null);
        }
        catch (ProjectOperationException exception) when (exception.Code == ProjectOperationErrorCode.InvalidManifest)
        {
            // MissionPackageBuilder reports an unusable shipped package this way. No marker was
            // written, so nothing downstream can pin it.
            return new ManagedChatProjectResolution(ManagedChatProjectOutcome.PackageUnavailable, null, Guid.Empty, exception.Message);
        }
        catch (ProjectOperationException exception)
        {
            return new ManagedChatProjectResolution(ManagedChatProjectOutcome.InvalidProject, null, Guid.Empty, exception.Message);
        }
    }

    private string HomeFor(Guid projectId) => Path.Combine(projects.ProjectsRoot, projectId.ToString("D"));

    private static Guid? FindShippedMissionId(ProjectRecord record) =>
        (record.Manifest.MissionDefinitions ?? [])
            .Where(item => string.Equals(item.Name, ShippedMissionCatalog.MissionName, StringComparison.OrdinalIgnoreCase))
            .Select(item => (Guid?)item.MissionId)
            .SingleOrDefault();

    // The same validation Host and Worker each perform independently, so an unusable shipped
    // package is a resolution outcome rather than a surprise at admission time.
    private static bool Unusable(DurableMissionLaunch launch) =>
        launch.Package is not { } package || !DurableMissionPackageValidator.TryValidate(
            new DurableMissionPackageInput(package.FormatVersion, package.PackageHash, package.MissionSource,
                package.RootMissionName, package.RootInputName,
                [.. package.ResolvedExperts.Select(expert => new DurableResolvedExpertInput(expert.Name, expert.LockSource,
                    expert.LockPath, expert.LockHash, expert.ExpertMarkdown))]), out _, out _);

    private static ManagedChatProjectResolution Invalid(string home, string reason) =>
        new(ManagedChatProjectOutcome.InvalidProject, home, Guid.Empty, reason);
}
