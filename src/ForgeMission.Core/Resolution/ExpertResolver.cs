using ForgeMission.Core.Experts;

namespace ForgeMission.Core.Resolution;

// Resolves expert names to ExpertDefinition objects using the two-source order:
//   1. <mission-dir>/experts/<Name>/expert.md  — local, always wins
//   2. the lock file's recorded source — a project:/// file under the mission directory, or the
//      cache location derived from an immutable oci://…@sha256:… source (43.20 task 3)
//
// Digest verification: if the lock file entry has a content digest, the loaded file must match.
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
            var source   = ExpertSource.Parse(entry.Source, name);
            var recorded = RecordedPath(source, missionDir);
            var (absPath, label) = Locate(name, recorded, missionDir);

            if (absPath is null)
                throw new ExpertLoadException(
                    $"MCL008 Expert '{name}' not found at '{Describe(source)}'. Run 'forge init' to regenerate the lock file.");

            // Warn when a local expert shadows a non-local (OCI/cache) entry in the lock file.
            // Local wins intentionally — but silent shadowing is a reproducibility risk.
            var isShadowing = !string.Equals(absPath, recorded, StringComparison.OrdinalIgnoreCase);
            if (isShadowing && File.Exists(recorded))
            {
                warnings?.WriteLine(
                    $"warning MCL010: local expert '{name}' shadows the lock-file entry " +
                    $"({AbbrPath(recorded)}). Local version will be used. " +
                    $"Run 'forge init' to update the lock file if this is intentional.");
            }

            // Digest verification — skipped for a migrated legacy entry that recorded no digest,
            // and when a local expert shadows the recorded one, since the digest describes the
            // recorded file rather than the local override.
            if (!isShadowing && entry.ContentDigest is { Length: > 0 } expected)
            {
                var actual = LockFileIO.ComputeContentDigest(absPath);
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    throw new ExpertLoadException(
                        $"MCL009 Expert '{name}' has changed since last 'forge init'. " +
                        $"Run 'forge init' to update the lock file.");
            }

            verbose?.WriteLine($"[forge] {name,-30} → {label,-8} ({AbbrPath(absPath)})");

            result[name] = ExpertLoader.ParseFile(absPath);
        }

        return result;
    }

    /// <summary>Where the lock file says this expert lives. A project source resolves under the
    /// mission directory; an OCI source resolves to the one cache location its immutable digest
    /// derives — never to a path the file recorded, because it records none.</summary>
    public static string RecordedPath(ExpertSource source, string missionDir) =>
        source.Kind == ExpertSourceKind.Project
            ? Path.GetFullPath(Path.Combine(missionDir, source.ProjectRelativePath))
            : ForgeCache.ExpertMdPath(source);

    // Returns (absolutePath, sourceLabel), or (null, "") when the expert is at neither location.
    private static (string? Path, string Label) Locate(string name, string recorded, string missionDir)
    {
        // 1. Local expert directory always wins over the recorded location
        var localPath = Path.GetFullPath(
            Path.Combine(missionDir, SourceResolver.DefaultExpertsDir, name, "expert.md"));
        if (File.Exists(localPath))
            return (localPath, "local");

        // 2. The lock file's recorded location
        if (File.Exists(recorded))
            return (recorded, recorded.StartsWith(ForgeCache.ExpertsRoot, StringComparison.Ordinal) ? "cache" : "local");

        return (null, "");
    }

    // An OCI expert is named by its source, not by a cache path the user never chose.
    private static string Describe(ExpertSource source) =>
        source.Kind == ExpertSourceKind.Project ? source.ProjectRelativePath : source.Value;

    // Abbreviate the path for verbose output: ~/... for home-relative, relative for mission-relative.
    private static string AbbrPath(string absPath)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (absPath.StartsWith(home, StringComparison.Ordinal))
            return "~" + absPath[home.Length..].Replace(Path.DirectorySeparatorChar, '/');
        return absPath;
    }
}
