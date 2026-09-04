namespace ForgeMission.Core.Resolution;

/// <summary>
/// Resolves paths inside the global forge cache (~/.forge).
/// Uses Environment.SpecialFolder.UserProfile for cross-platform home directory resolution.
/// </summary>
public static class ForgeCache
{
    private static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".forge");

    /// <summary>
    /// Returns the absolute path where an OCI expert's expert.md should be cached.
    /// Layout: ~/.forge/experts/{registry}/{name}/{version}/expert.md
    /// </summary>
    public static string ExpertsRoot => Path.Combine(Root, "experts");

    public static string ExpertMdPath(string registry, string ociName, string version)
        => Path.Combine(Root, "experts", registry, ociName, version, "expert.md");

    /// <summary>
    /// Where an OCI-sourced expert materializes, derived entirely from its parsed immutable source
    /// (43.20 task 3): <c>~/.forge/experts/{registry}/{repository}/sha256/{hex}/expert.md</c>.
    ///
    /// The derivation is one-way and this is the only place it happens. A lock file records the
    /// source, never this path, so the path is a machine-local detail that cannot leak into a
    /// portable artifact — and because it is keyed by the manifest digest rather than a tag, two
    /// different artifacts can never collide on one cache location.
    ///
    /// The containment check is not defensive decoration: the registry and repository come from a
    /// file on disk, so a hand-edited source is untrusted input. <see cref="ExpertSource"/> already
    /// refuses a traversal segment; this asserts the outcome rather than trusting that it did.
    /// </summary>
    public static string ExpertMdPath(ExpertSource source)
    {
        if (source.Kind != ExpertSourceKind.Oci)
            throw new ArgumentException(
                $"Only an OCI expert source materializes in the cache; got {source.Kind}.", nameof(source));

        var root = Path.GetFullPath(ExpertsRoot);
        var digestSegment = source.ManifestDigest.Replace(':', Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(
            root,
            source.Registry,
            source.Repository.Replace('/', Path.DirectorySeparatorChar),
            digestSegment,
            "expert.md"));

        return candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? candidate
            : throw new MclException(
                MclErrorCode.InvalidLockSource,
                $"'{source.Value}' derives a materialization path outside the expert cache.");
    }

    /// <summary>
    /// Directory a pulled OCI <b>mission</b> is unpacked into (Phase 39.4). A mission is
    /// self-contained (mission.mcl + lock + experts/**), so it caches as a directory, not a single
    /// file. Layout: <c>~/.forge/missions/{registry}/{name}/{version}/</c> — where <c>version</c> is
    /// typically an immutable <c>sha256:…</c> digest (digest-pinned pulls).
    /// </summary>
    public static string MissionsRoot => Path.Combine(Root, "missions");

    public static string MissionDir(string registry, string ociName, string version)
        => Path.Combine(Root, "missions", registry, ociName, Sanitize(version));

    // A digest reference ("sha256:abc…") contains ':' which is illegal in a Windows path segment
    // and awkward elsewhere — normalise to a safe directory name.
    private static string Sanitize(string version) => version.Replace(':', '-');
}
