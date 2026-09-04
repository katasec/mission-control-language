using System.Text;
using ForgeMission.ClientRuntime.Transport;
using ForgeMission.Core.Resolution;

namespace ForgeMission.ClientRuntime.Services;

/// <summary>
/// The Project Explorer's one read (43.20 task 3): manifest + <c>mcl.lock</c> in, a typed
/// projection out. It is the only place a Project's local layout is turned into something a
/// surface can render, and the only place an entry is turned back into a file.
///
/// Two rules hold everything else up:
///
/// 1. <b>No path leaves here.</b> Entries carry an opaque ID, a display name, a kind, and — for a
///    resolved OCI dependency only — the pinned reference that IS the evidence being shown. A
///    surface therefore has nothing it could widen into a file read, which is what makes
///    "Presentation cannot touch the filesystem" structural rather than a rule to remember.
/// 2. <b>An entry ID is matched, never interpreted.</b> Opening a document rebuilds the projection
///    and looks the ID up in it. There is no server-side entry cache to go stale and no id-to-path
///    decoding, so a forged or outdated ID resolves to nothing rather than to a file.
///
/// This displays already-resolved records. It calls no registry, resolves no dependency, and
/// writes nothing.
/// </summary>
internal sealed class ProjectWorkbenchProjector(ProjectStore projects)
{
    /// <summary>A document must be presentable as text in a UI that has no other renderer, so the
    /// cap is small and checked as a byte length before anything is read into memory.</summary>
    private const long MaxDocumentBytes = 1024 * 1024;

    private const string LockFileName = "mcl.lock";
    private const string TextContentType = "text/plain";

    public ProjectWorkbenchProjection Project(string home)
    {
        var (summary, entries) = Read(home);
        return new ProjectWorkbenchProjection(
            summary,
            [.. entries.Where(entry => entry.IsAsset).Select(entry => entry.Listed)],
            [.. entries.Where(entry => entry.IsContext).Select(entry => entry.Listed)],
            [.. entries.Where(entry => entry.IsRun).Select(entry => entry.Listed)]);
    }

    public ProjectDocument OpenDocument(string home, string entryId)
    {
        var (_, entries) = Read(home);
        var entry = entries.FirstOrDefault(candidate =>
            string.Equals(candidate.Listed.EntryId, entryId, StringComparison.Ordinal));

        // A run, a source root, or an attached artifact has no document surface in this task, so
        // it is refused exactly like an unknown ID rather than being given a stub view.
        if (entry?.Path is not { } path)
            throw new ProjectOperationException(ProjectOperationErrorCode.DocumentNotFound,
                "That item is no longer part of this Project.");

        return new ProjectDocument(entry.Listed.DisplayName, TextContentType, ReadText(path, entry.Listed.DisplayName));
    }

    // --- reading ---------------------------------------------------------------------------------

    // One read serves both operations, so a document can never be opened against a projection the
    // caller could not also have seen.
    private (ProjectSummary Summary, List<WorkbenchEntry> Entries) Read(string home)
    {
        var project = projects.ReadForHome(home);
        var manifest = project.Manifest;

        List<WorkbenchEntry> entries =
        [
            .. ManifestAssets(manifest, project.Home),
            .. Dependencies(project.Home),
            .. manifest.AttachedContext.Select(ContextEntry),
            .. manifest.Runs.Select(RunEntry),
        ];

        var summary = new ProjectSummary(manifest.ProjectId, manifest.Title, manifest.Goal, project.Home);
        return (summary, entries);
    }

    // Assets the manifest declares. ProjectStore has already refused any path that escapes the
    // home, so the remaining question is only whether the file the manifest promises is there — a
    // manifest that describes a Project state which does not exist is reported, never rendered as
    // a shorter list that silently omits it.
    private static IEnumerable<WorkbenchEntry> ManifestAssets(ProjectManifest manifest, string home)
    {
        foreach (var asset in manifest.Assets)
        {
            var path = Path.GetFullPath(Path.Combine(home, asset.RelativePath));
            if (!File.Exists(path))
                throw new ProjectOperationException(ProjectOperationErrorCode.InvalidManifest,
                    $"This Project declares {asset.RelativePath}, but that file is not present.");

            yield return new WorkbenchEntry(
                new ProjectExplorerEntry(
                    $"asset:{asset.RelativePath}",
                    asset.RelativePath,
                    AssetKind(asset.Kind),
                    IsReadOnly: false),
                path);
        }
    }

    // Resolved expert dependencies. The lock file is optional: a Project that has none simply has
    // no dependency evidence, which is an empty section rather than a failure. When it IS present
    // every record must be presentable honestly — an unreadable lock, an unresolvable source, a
    // missing materialization, or content that no longer matches its recorded digest fails the
    // whole projection, because a partial list would read as "these are the dependencies".
    private static IEnumerable<WorkbenchEntry> Dependencies(string home)
    {
        var lockPath = Path.Combine(home, LockFileName);
        if (!File.Exists(lockPath))
            return [];

        LockFile lockFile;
        try
        {
            lockFile = LockFileIO.Read(lockPath);
        }
        catch (Exception exception) when (exception is MclException or IOException or UnauthorizedAccessException)
        {
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidDependency, exception.Message);
        }

        return [.. lockFile.Experts.Select(expert => Dependency(expert.Key, expert.Value, home))];
    }

    private static WorkbenchEntry Dependency(string name, LockFileExpert entry, string home)
    {
        ExpertSource source;
        try
        {
            source = ExpertSource.Parse(entry.Source, name);
        }
        catch (MclException exception)
        {
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidDependency, exception.Message);
        }

        var path = Materialization(name, source, home);
        if (!File.Exists(path))
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidDependency,
                $"Expert '{name}' is recorded in {LockFileName} but is not present. Run 'forge init' to resolve it again.");

        // A recorded digest that no longer matches means the file changed after it was locked, so
        // the pinned evidence would be a lie. A legacy entry that recorded no digest is left
        // unverified rather than being failed for a fact it never claimed.
        if (entry.ContentDigest is { Length: > 0 } expected &&
            !string.Equals(LockFileIO.ComputeContentDigest(path), expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidDependency,
                $"Expert '{name}' has changed since it was locked. Run 'forge init' to update {LockFileName}.");
        }

        var isOci = source.Kind == ExpertSourceKind.Oci;
        return new WorkbenchEntry(
            new ProjectExplorerEntry(
                $"dep:{name}",
                name,
                isOci ? ProjectExplorerEntryKind.OciDependency : ProjectExplorerEntryKind.Expert,
                IsReadOnly: isOci,
                // Only a registry-resolved dependency shows its source: that pinned
                // reference+digest is the evidence. A project-sourced expert would only be
                // showing a local path, which must not cross this boundary.
                Source: isOci ? source.Value : null),
            path);
    }

    // The cache location is derived here and used here. It is never returned, and it is asserted
    // to sit under the expert cache root by ForgeCache itself before it is used to read anything.
    private static string Materialization(string name, ExpertSource source, string home)
    {
        try
        {
            return ExpertResolver.RecordedPath(source, home);
        }
        catch (MclException exception)
        {
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidDependency,
                $"Expert '{name}': {exception.Message}");
        }
    }

    // Context and runs are listed, not opened: a source root is a directory, an artifact lives in
    // the Conversation service, and a run's Trace is its own later surface. They carry no path,
    // and an attached SourceRoot/File reference is an ABSOLUTE local path in the manifest — which
    // is exactly why only its display name is projected.
    private static WorkbenchEntry ContextEntry(ProjectContextDescriptor context) =>
        new(new ProjectExplorerEntry(
            $"context:{context.Id}",
            context.DisplayName,
            ContextKind(context.Kind),
            IsReadOnly: true),
            Path: null);

    private static WorkbenchEntry RunEntry(ProjectRunMetadata run) =>
        new(new ProjectExplorerEntry(
            $"run:{run.RunId}",
            run.Title,
            ProjectExplorerEntryKind.Run,
            IsReadOnly: true),
            Path: null);

    // --- document content ------------------------------------------------------------------------

    // Size, then bytes, then text. The order is the point: the cap is a file length, so an
    // oversized document is refused without ever being read, and the decode is strict so invalid
    // UTF-8 is refused rather than silently rendered as replacement characters.
    private static string ReadText(string path, string displayName)
    {
        byte[] bytes;
        try
        {
            var length = new FileInfo(path).Length;
            if (length > MaxDocumentBytes)
                throw new ProjectOperationException(ProjectOperationErrorCode.InvalidDocument,
                    $"{displayName} is too large to open here.");

            bytes = File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ProjectOperationException(ProjectOperationErrorCode.DocumentNotFound,
                $"{displayName} could not be read.");
        }

        // A NUL byte is decodable UTF-8 but never text a person meant to read, so the strict
        // decode below would let it through.
        if (bytes.Contains<byte>(0))
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidDocument,
                $"{displayName} is not a text document.");

        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new ProjectOperationException(ProjectOperationErrorCode.InvalidDocument,
                $"{displayName} is not valid UTF-8 text.");
        }
    }

    // --- kinds -------------------------------------------------------------------------------------

    private static ProjectExplorerEntryKind AssetKind(ProjectAssetKind kind) => kind switch
    {
        ProjectAssetKind.Mission => ProjectExplorerEntryKind.Mission,
        ProjectAssetKind.Expert => ProjectExplorerEntryKind.Expert,
        _ => ProjectExplorerEntryKind.LockFile,
    };

    private static ProjectExplorerEntryKind ContextKind(ProjectContextKind kind) => kind switch
    {
        ProjectContextKind.SourceRoot => ProjectExplorerEntryKind.SourceRoot,
        ProjectContextKind.File => ProjectExplorerEntryKind.File,
        _ => ProjectExplorerEntryKind.Artifact,
    };

    /// <summary>One listed item and, for the openable kinds only, where it actually lives. The
    /// path stays on this side of the boundary: <see cref="ProjectExplorerEntry"/> is what crosses
    /// it, and it has nowhere to put one.</summary>
    private sealed record WorkbenchEntry(ProjectExplorerEntry Listed, string? Path)
    {
        public bool IsAsset => Listed.Kind
            is ProjectExplorerEntryKind.Mission
            or ProjectExplorerEntryKind.Expert
            or ProjectExplorerEntryKind.LockFile
            or ProjectExplorerEntryKind.OciDependency;

        public bool IsContext => Listed.Kind
            is ProjectExplorerEntryKind.SourceRoot
            or ProjectExplorerEntryKind.File
            or ProjectExplorerEntryKind.Artifact;

        public bool IsRun => Listed.Kind is ProjectExplorerEntryKind.Run;
    }
}
