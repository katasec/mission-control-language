namespace ForgeMission.Rooms;

/// <summary>
/// Official handles reserved before the namespace opens to custom/marketplace agents (38.5
/// task 6). Handles are bare, globally-unique, and first-come-first-served (the "X model"), so
/// the platform's own names must be claimed up front — cheap insurance against impersonation
/// while there is no collision risk yet.
/// <para>
/// This is the canonical reserved set: handle-claiming logic (39.5) consults <see cref="IsReserved"/>
/// to reject anyone trying to register a reserved handle they don't own. Matching is
/// case-insensitive and leading-<c>@</c> tolerant.
/// </para>
/// </summary>
public static class ReservedHandles
{
    /// <summary>The reserved official handles (bare form, with leading <c>@</c>).</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "@assistant",
        "@guard",
        "@claude",
        "@openai",
        "@grok",
    };

    /// <summary>True if <paramref name="handle"/> is a reserved official handle.</summary>
    public static bool IsReserved(string? handle) =>
        !string.IsNullOrWhiteSpace(handle) && All.Contains(Normalize(handle));

    /// <summary>Lower-cases and ensures a single leading <c>@</c> for comparison.</summary>
    private static string Normalize(string handle)
    {
        var trimmed = handle.Trim();
        return trimmed.StartsWith('@') ? trimmed : "@" + trimmed;
    }
}
