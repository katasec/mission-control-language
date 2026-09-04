namespace ForgeMission.Core.Resolution;

/// <summary>
/// The canonical identity of one resolved expert, parsed from a lock file's <c>source</c> URI
/// (43.20 task 3).
///
/// One URI is the whole identity, in both cases, and its SYNTAX selects the resolver — there is no
/// nullable sibling field whose value changes what a path means. That is the point: the previous
/// <c>{ source, path, hash }</c> shape made <c>path</c> mean either a Project-relative file or a
/// machine-local cache location depending on <c>source</c>, so a lock file was not portable and
/// could not be read without also knowing which machine wrote it.
///
/// A materialization path is DERIVED from a parsed source (see <see cref="ForgeCache"/>); it is
/// never stored here, never serialized, and never accepted from a caller.
/// </summary>
public sealed record ExpertSource
{
    public const string ProjectScheme = "project";
    public const string OciScheme = "oci";

    private const string ProjectPrefix = $"{ProjectScheme}:///";
    private const string OciPrefix = $"{OciScheme}://";
    private const string DigestPrefix = "sha256:";
    private const int DigestLength = 71; // "sha256:" + 64 hex

    private ExpertSource(
        ExpertSourceKind kind, string value, string projectRelativePath,
        string registry, string repository, string manifestDigest)
    {
        Kind = kind;
        Value = value;
        ProjectRelativePath = projectRelativePath;
        Registry = registry;
        Repository = repository;
        ManifestDigest = manifestDigest;
    }

    public ExpertSourceKind Kind { get; }

    /// <summary>The canonical URI exactly as it is written in the lock file.</summary>
    public string Value { get; }

    /// <summary>Normalized POSIX Project-relative path. Empty for an OCI source.</summary>
    public string ProjectRelativePath { get; }

    /// <summary>Registry authority. Empty for a Project source.</summary>
    public string Registry { get; }

    /// <summary>Repository path within the registry. Empty for a Project source.</summary>
    public string Repository { get; }

    /// <summary>Immutable <c>sha256:…</c> manifest digest. Empty for a Project source. This is the
    /// artifact's identity, deliberately distinct from the content digest of the resolved
    /// <c>expert.md</c> that the lock file records beside it.</summary>
    public string ManifestDigest { get; }

    // --- parsing --------------------------------------------------------------------------------

    /// <summary>Parses a lock file <c>source</c> value, or throws the named diagnostic. A source
    /// that does not parse is never guessed at or partly honoured.</summary>
    public static ExpertSource Parse(string? source, string expertName)
    {
        if (TryParse(source, out var parsed))
            return parsed;

        throw new MclException(
            MclErrorCode.InvalidLockSource,
            $"Expert '{expertName}' has an unusable lock file source: '{source}'.",
            $"A source is either '{ProjectPrefix}<path within the project>' or " +
            $"'{OciPrefix}<registry>/<repository>@{DigestPrefix}<64 hex>'. Run 'forge init' to regenerate the lock file.");
    }

    public static bool TryParse(string? source, out ExpertSource parsed)
    {
        parsed = null!;
        if (string.IsNullOrWhiteSpace(source) || !string.Equals(source, source.Trim(), StringComparison.Ordinal))
            return false;

        if (source.StartsWith(ProjectPrefix, StringComparison.Ordinal))
            return TryParseProject(source, source[ProjectPrefix.Length..], out parsed);

        if (source.StartsWith(OciPrefix, StringComparison.Ordinal))
            return TryParseOci(source, source[OciPrefix.Length..], out parsed);

        return false;
    }

    // --- construction ---------------------------------------------------------------------------

    /// <summary>Builds the canonical Project source URI for a mission-relative path. Rejects the
    /// same shapes <see cref="TryParse"/> does, so a writer can never emit a source its own reader
    /// would refuse.</summary>
    public static string ForProjectPath(string relativePath)
    {
        var posix = (relativePath ?? "").Replace('\\', '/');
        var candidate = ProjectPrefix + posix;
        return TryParse(candidate, out _)
            ? candidate
            : throw new MclException(
                MclErrorCode.InvalidLockSource,
                $"'{relativePath}' cannot be recorded as a project-relative expert source.");
    }

    /// <summary>Builds the canonical OCI source URI. The digest is the registry's immutable
    /// manifest digest, never a tag.</summary>
    public static string ForOci(string registry, string repository, string manifestDigest)
    {
        var candidate = $"{OciPrefix}{registry}/{repository}@{manifestDigest}";
        return TryParse(candidate, out _)
            ? candidate
            : throw new MclException(
                MclErrorCode.InvalidLockSource,
                $"'{candidate}' is not a usable immutable OCI expert source.");
    }

    /// <summary>True for a full lower-case <c>sha256:&lt;64 hex&gt;</c> digest — the only form
    /// either a source or a content digest may take.</summary>
    public static bool IsContentDigest(string? digest) =>
        digest is { Length: DigestLength } &&
        digest.StartsWith(DigestPrefix, StringComparison.Ordinal) &&
        IsLowerHex(digest.AsSpan(DigestPrefix.Length));

    // --- private --------------------------------------------------------------------------------

    // "project" has no authority, so everything after the single leading slash is the path. It must
    // stay inside the Project: no traversal, no absolute path, no drive or UNC root, and no empty
    // or dot-only segment that would resolve to the Project home itself.
    private static bool TryParseProject(string value, string path, out ExpertSource parsed)
    {
        parsed = null!;
        if (path.Length == 0 || path.StartsWith('/') || path.Contains(':', StringComparison.Ordinal))
            return false;

        foreach (var segment in path.Split('/'))
        {
            if (segment.Length == 0 || segment == "." || segment == "..")
                return false;
        }

        parsed = new ExpertSource(ExpertSourceKind.Project, value, path, "", "", "");
        return true;
    }

    // "oci://<registry>/<repository>@sha256:<hex>". The digest is split off FIRST: a repository may
    // legitimately contain slashes but never an '@', so a second '@' anywhere means the reference
    // is ambiguous and is refused rather than resolved by guessing.
    private static bool TryParseOci(string value, string rest, out ExpertSource parsed)
    {
        parsed = null!;
        var at = rest.IndexOf('@');
        if (at < 0 || rest.IndexOf('@', at + 1) >= 0)
            return false;

        var digest = rest[(at + 1)..];
        if (!IsContentDigest(digest))
            return false;

        var location = rest[..at];
        var slash = location.IndexOf('/');
        if (slash <= 0 || slash == location.Length - 1)
            return false;

        var registry = location[..slash];
        var repository = location[(slash + 1)..];
        if (registry.Contains('/', StringComparison.Ordinal) || !IsSafeRepository(repository))
            return false;

        parsed = new ExpertSource(ExpertSourceKind.Oci, value, "", registry, repository, digest);
        return true;
    }

    // The repository becomes directory segments under the expert cache, so the same traversal rule
    // the Project path gets applies here — a registry cannot name a path that escapes the cache.
    private static bool IsSafeRepository(string repository)
    {
        foreach (var segment in repository.Split('/'))
        {
            if (segment.Length == 0 || segment == "." || segment == ".." ||
                segment.Contains(':', StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static bool IsLowerHex(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (!char.IsAsciiDigit(character) && (character < 'a' || character > 'f'))
                return false;
        }

        return true;
    }
}

public enum ExpertSourceKind
{
    /// <summary>Resolved from the Project itself, relative to the mission directory.</summary>
    Project,

    /// <summary>Resolved from a registry artifact pinned to one immutable manifest digest.</summary>
    Oci,
}
