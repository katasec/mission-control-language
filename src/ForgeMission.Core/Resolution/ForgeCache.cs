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
