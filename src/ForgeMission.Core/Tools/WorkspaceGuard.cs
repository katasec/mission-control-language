namespace ForgeMission.Core.Tools;

// Confines Read/Edit/Write to the workspace root (43.2's opened folder). A violation returns a
// tool-facing error string, never a thrown exception — 42.3's graceful-unknown-tool discipline
// ("answer with an error tool_result, never a crash") applies to path violations too.
public static class WorkspaceGuard
{
    // Case-sensitive comparison only where the filesystem actually is: Windows and macOS are
    // case-insensitive by default, so treating differently-cased paths as distinct there would
    // just produce false rejections, not extra safety. Linux is case-sensitive — collapsing case
    // there could let a differently-cased sibling path outside the root pass as "inside" it.
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    public static bool TryResolve(string workspaceRoot, string requestedPath, out string resolved, out string? error)
    {
        resolved = "";

        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            error = "file_path is required";
            return false;
        }

        var rootFull = RealPath(Path.GetFullPath(workspaceRoot));
        var candidate = Path.IsPathRooted(requestedPath)
            ? requestedPath
            : Path.Combine(rootFull, requestedPath);
        var full = RealPath(Path.GetFullPath(candidate));

        if (!IsInsideRoot(full, rootFull))
        {
            error = $"'{requestedPath}' resolves outside the workspace root ({workspaceRoot})";
            return false;
        }

        resolved = full;
        error = null;
        return true;
    }

    private static bool IsInsideRoot(string full, string rootFull)
    {
        if (full.Equals(rootFull, PathComparison)) return true;

        var boundary = rootFull.EndsWith(Path.DirectorySeparatorChar) ? rootFull : rootFull + Path.DirectorySeparatorChar;
        return full.StartsWith(boundary, PathComparison);
    }

    // Follows symlinks on every EXISTING ancestor — including the root itself — so a symlinked
    // directory can't be used to escape confinement, even for a path that doesn't exist yet
    // (Write creating a brand-new file under a symlinked parent still resolves through the real
    // target first).
    private static string RealPath(string fullPath)
    {
        if (File.Exists(fullPath))
            return ResolveLink(new FileInfo(fullPath)) ?? fullPath;
        if (Directory.Exists(fullPath))
            return ResolveLink(new DirectoryInfo(fullPath)) ?? fullPath;

        var parent = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(parent) || parent == fullPath)
            return fullPath; // hit a filesystem root — nothing left to resolve

        return Path.Combine(RealPath(parent), Path.GetFileName(fullPath));
    }

    private static string? ResolveLink(FileSystemInfo info) => info.ResolveLinkTarget(returnFinalTarget: true)?.FullName;
}
