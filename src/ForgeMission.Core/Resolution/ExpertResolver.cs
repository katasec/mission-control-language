using ForgeMission.Core.Experts;

namespace ForgeMission.Core.Resolution;

// Resolves expert names to ExpertDefinition objects using the two-source order:
//   1. <mission-dir>/experts/<Name>/expert.md  — local, always wins
//   2. Lock file recorded path (may point to OCI cache at ~/.forge/experts/...)
//
// Hash verification: if the lock file entry has a hash, the loaded file must match.
// If it doesn't match → the expert was modified since last 'forge init'.
//
// Verbose mode logs resolution source + path for each expert to the provided writer.
// Warnings are always emitted to stderr (independent of verbose flag).
public static class ExpertResolver
{
    public static Dictionary<string, ExpertDefinition> ResolveAll(
        LockFile      lockFile,
        string        missionDir,
        TextWriter?   verbose  = null,
        TextWriter?   warnings = null)
    {
        var result = new Dictionary<string, ExpertDefinition>(StringComparer.Ordinal);

        foreach (var (name, entry) in lockFile.Experts)
        {
            var (absPath, source) = Locate(name, entry, missionDir);

            if (absPath is null)
                throw new ExpertLoadException(
                    $"MCL008 Expert '{name}' not found at '{entry.Path}'. Run 'forge init' to regenerate the lock file.");

            // Warn when a local expert shadows a non-local (OCI/cache) entry in the lock file.
            // Local wins intentionally — but silent shadowing is a reproducibility risk.
            if (source == "local" && !string.IsNullOrEmpty(entry.Path))
            {
                var recorded = ResolveLockPath(entry.Path, missionDir);
                if (!string.Equals(absPath, recorded, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(recorded))
                {
                    warnings?.WriteLine(
                        $"warning MCL010: local expert '{name}' shadows the lock-file entry " +
                        $"({AbbrPath(recorded)}). Local version will be used. " +
                        $"Run 'forge init' to update the lock file if this is intentional.");
                }
            }

            // Hash verification — skip for legacy lock files without a recorded hash.
            // Also skip when a local expert shadows a non-local entry: the lock hash
            // is for the non-local version and does not apply to the local override.
            var isShadowing = source == "local" && !string.IsNullOrEmpty(entry.Path)
                && !string.Equals(absPath, ResolveLockPath(entry.Path, missionDir), StringComparison.OrdinalIgnoreCase);
            if (!isShadowing && entry.Hash is { Length: > 0 } expectedHash)
            {
                var actualHash = LockFileIO.ComputeHash(absPath);
                if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new ExpertLoadException(
                        $"MCL009 Expert '{name}' has changed since last 'forge init'. " +
                        $"Run 'forge init' to update the lock file.");
            }

            verbose?.WriteLine($"[forge] {name,-30} → {source,-8} ({AbbrPath(absPath)})");

            result[name] = ExpertLoader.ParseFile(absPath);
        }

        return result;
    }

    // Returns (absolutePath, sourceLabel) using the resolution order.
    // Returns (null, "") if the expert cannot be found at any source.
    private static (string? Path, string Source) Locate(string name, LockFileExpert entry, string missionDir)
    {
        // 1. Local expert directory always wins over the recorded lock path
        var localPath = Path.Combine(missionDir, SourceResolver.DefaultExpertsDir, name, "expert.md");
        if (File.Exists(localPath))
            return (localPath, "local");

        // 2. Lock file recorded path (relative → absolute, or ~/... → home-relative)
        if (!string.IsNullOrEmpty(entry.Path))
        {
            var recorded = ResolveLockPath(entry.Path, missionDir);
            if (File.Exists(recorded))
                return (recorded, entry.Source == SourceResolver.DefaultExpertsDir ? "local" : "cache");
        }

        return (null, "");
    }

    private static string ResolveLockPath(string path, string missionDir)
    {
        if (path.StartsWith("~/", StringComparison.Ordinal))
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                path[2..].Replace('/', Path.DirectorySeparatorChar));

        if (Path.IsPathRooted(path))
            return path;

        return Path.GetFullPath(Path.Combine(missionDir, path));
    }

    // Abbreviate the path for verbose output: ~/... for home-relative, relative for mission-relative.
    private static string AbbrPath(string absPath)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (absPath.StartsWith(home, StringComparison.Ordinal))
            return "~" + absPath[home.Length..].Replace(Path.DirectorySeparatorChar, '/');
        return absPath;
    }
}
