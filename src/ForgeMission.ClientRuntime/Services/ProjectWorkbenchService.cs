using System.Security.Cryptography;
using System.Text;
using ForgeMission.ClientRuntime.Transport;

namespace ForgeMission.ClientRuntime.Services;

/// <summary>
/// Owns the small local Explorer projection. Entry IDs are opaque Runtime values resolved against
/// a freshly read manifest; callers never pass a file path. It neither crawls source roots nor
/// resolves dependencies, registry data, OCI content, or network resources.
/// </summary>
internal sealed class ProjectWorkbenchService(ProjectStore projects)
{
    private const int MaximumDocumentBytes = 1024 * 1024;

    public async Task<SelectProjectMissionResponse> SelectMissionAsync(string home, string mission, CancellationToken ct)
    {
        try
        {
            var project = await projects.SelectMissionAsync(home, mission, ct);
            return new SelectProjectMissionResponse(Missions(project.Manifest), null);
        }
        catch (ProjectOperationException exception)
        {
            return new SelectProjectMissionResponse(null, ProjectMissionApplication.ToError(exception));
        }
    }

    public GetProjectWorkbenchResponse GetProjection(string home)
    {
        try
        {
            var project = projects.ReadForHome(home);
            return new GetProjectWorkbenchResponse(BuildProjection(project), null);
        }
        catch (ProjectOperationException exception)
        {
            return new GetProjectWorkbenchResponse(null, ProjectMissionApplication.ToError(exception));
        }
    }

    public OpenProjectDocumentResponse OpenDocument(string home, string entryId)
    {
        try
        {
            var project = projects.ReadForHome(home);
            return Open(project, entryId);
        }
        catch (ProjectOperationException exception)
        {
            return new OpenProjectDocumentResponse(null, ProjectMissionApplication.ToError(exception));
        }
    }

    private static ProjectWorkbenchProjection BuildProjection(ProjectRecord project)
    {
        var assets = project.Manifest.Assets
            .Select((asset, index) => new ProjectWorkbenchEntry(AssetId(index), asset.RelativePath, asset.Kind.ToString()))
            .ToList();
        var lockPath = Path.Combine(project.Home, "mcl.lock");
        if (File.Exists(lockPath) && assets.All(asset => !string.Equals(asset.Label, "mcl.lock", StringComparison.Ordinal)))
            assets.Add(new ProjectWorkbenchEntry("lock", "mcl.lock", "LockFile"));
        var context = project.Manifest.AttachedContext
            .Select((item, index) => new ProjectWorkbenchEntry(ContextId(index), item.DisplayName, item.Kind.ToString()))
            .ToArray();
        return new ProjectWorkbenchProjection(assets, context);
    }

    private static OpenProjectDocumentResponse Open(ProjectRecord project, string entryId)
    {
        if (entryId == "lock")
            return ReadFile(project.Home, "mcl.lock", expectedHash: null, "mcl.lock");
        if (TryIndex(entryId, "asset:", project.Manifest.Assets.Length, out var assetIndex))
        {
            var asset = project.Manifest.Assets[assetIndex];
            return ReadFile(project.Home, asset.RelativePath, asset.ContentHash, asset.RelativePath);
        }
        if (TryIndex(entryId, "context:", project.Manifest.AttachedContext.Length, out var contextIndex))
        {
            var context = project.Manifest.AttachedContext[contextIndex];
            return context.Kind == ProjectContextKind.File
                ? ReadAbsoluteFile(context.Reference, context.ContentHash, context.DisplayName, checkAncestors: true)
                : PlainText(context.DisplayName, context.Reference);
        }
        return Failure(ProjectOperationErrorCode.DocumentUnavailable, "That Project document is no longer available.");
    }

    private static OpenProjectDocumentResponse ReadFile(string home, string relativePath, string? expectedHash, string label)
    {
        if (Path.IsPathRooted(relativePath))
            return Failure(ProjectOperationErrorCode.DocumentUnavailable, "That Project document is outside the Project home.");
        var root = Path.GetFullPath(home);
        var path = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || HasLinkInPath(root, path))
            return Failure(ProjectOperationErrorCode.DocumentUnavailable, "That Project document is unavailable.");
        return ReadAbsoluteFile(path, expectedHash, label, checkAncestors: false);
    }

    private static OpenProjectDocumentResponse ReadAbsoluteFile(
        string path, string? expectedHash, string label, bool checkAncestors)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.LinkTarget is not null || checkAncestors && HasLinkedAncestor(path))
                return Failure(ProjectOperationErrorCode.DocumentUnavailable, "That Project document is unavailable.");
            if (info.Length > MaximumDocumentBytes)
                return Failure(ProjectOperationErrorCode.DocumentTooLarge, "That Project document is larger than 1 MiB.");
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length > MaximumDocumentBytes)
                return Failure(ProjectOperationErrorCode.DocumentTooLarge, "That Project document is larger than 1 MiB.");
            var bytes = new byte[file.Length];
            file.ReadExactly(bytes);
            if (file.ReadByte() != -1)
                return Failure(ProjectOperationErrorCode.DocumentChanged, "That Project document changed while Forge was reading it.");
            if (bytes.Any(value => value == 0))
                return Failure(ProjectOperationErrorCode.DocumentBinary, "That Project document is binary and cannot be shown as text.");
            if (!string.IsNullOrEmpty(expectedHash) && !MatchesHash(bytes, expectedHash))
                return Failure(ProjectOperationErrorCode.DocumentChanged, "That Project document changed since it was recorded.");
            var content = new UTF8Encoding(false, true).GetString(bytes);
            return new OpenProjectDocumentResponse(new ProjectDocument(label, content, IsPlainText: false), null);
        }
        catch (DecoderFallbackException)
        {
            return Failure(ProjectOperationErrorCode.DocumentBinary, "That Project document is binary and cannot be shown as text.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Failure(ProjectOperationErrorCode.DocumentUnavailable, "That Project document is unavailable.");
        }
    }

    private static bool HasLinkInPath(string root, string path)
    {
        for (var current = new DirectoryInfo(Path.GetDirectoryName(path)!); current.FullName.Length >= root.Length; current = current.Parent!)
        {
            if (current.LinkTarget is not null) return true;
            if (string.Equals(current.FullName, root, StringComparison.Ordinal)) return false;
        }
        return true;
    }

    private static bool HasLinkedAncestor(string path)
    {
        for (var current = new DirectoryInfo(Path.GetDirectoryName(path)!); current is not null; current = current.Parent)
        {
            if (current.LinkTarget is not null) return true;
        }
        return false;
    }

    internal static bool MatchesHash(byte[] bytes, string expected) =>
        expected.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), expected["sha256:".Length..], StringComparison.OrdinalIgnoreCase);
    private static OpenProjectDocumentResponse PlainText(string label, string content) =>
        new(new ProjectDocument(label, content, IsPlainText: true), null);
    private static OpenProjectDocumentResponse Failure(ProjectOperationErrorCode code, string message) =>
        new(null, ProjectMissionApplication.Error(code, message));
    private static string AssetId(int index) => $"asset:{index}";
    private static string ContextId(int index) => $"context:{index}";
    private static bool TryIndex(string id, string prefix, int count, out int index)
    {
        index = -1;
        return id.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(id[prefix.Length..], out index) &&
            index >= 0 && index < count;
    }

    private static ProjectMissionsView Missions(ProjectManifest manifest) => new(ProjectMissions.All,
        manifest.SelectedMission is { Origin: ProjectMissionOrigin.BuiltIn } selected && ProjectMissions.IsAllowed(selected.Reference)
            ? selected.Reference : null,
        manifest.LegacyProjectControlConversationId is not null || manifest.MissionControlConversationId is not null);
}
