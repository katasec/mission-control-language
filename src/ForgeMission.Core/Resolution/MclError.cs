namespace ForgeMission.Core.Resolution;

public enum MclErrorCode
{
    UnknownExpert          = 1,
    DuplicateExpert        = 2,
    CircularReference      = 3,
    MissingFrontmatter     = 4,
    SourceNotFound         = 5,
    StaleLockFile          = 6,
    NotInitialised         = 7,
    OciNotPulled           = 10,
    OciPullFailed          = 11,

    // 43.20 task 3 — uniform expert source identity in mcl.lock v2.
    /// <summary>A lock file source is not a parseable <c>project:///</c> or immutable
    /// <c>oci://…@sha256:…</c> URI.</summary>
    InvalidLockSource      = 12,

    /// <summary>The lock file cannot be read forward honestly and must be regenerated — a v1 OCI
    /// entry, which records a cache path and a tag but no manifest digest, so its immutable source
    /// cannot be recovered without contacting the registry.</summary>
    LockFileNeedsReinit    = 13,
}

public class MclException(MclErrorCode code, string message, string? detail = null)
    : Exception($"MCL{(int)code:D3} {message}{(detail is null ? "" : $"\n\n{detail}")}")
{
    public MclErrorCode Code    { get; } = code;
    public string?      Detail  { get; } = detail;
}
