using ForgeMission.ClientRuntime.Transport;

namespace ForgeMission.ClientRuntime.Services;

/// <summary>
/// The closed catalog of missions a Project can run (43.21 task 1): exactly <c>Janus</c> and
/// <c>Naive</c>.
///
/// It is a fixed pair rather than a registry, a catalog lookup, or a configuration value, because
/// the product decision is that there are two — a person picks a named mission, never a provider,
/// a model, or an expert. Adding a third is a deliberate product change here, not a deployment
/// option someone can set.
///
/// This is the FIRST of three checks on the same value: Client Runtime allow-lists it before
/// persisting or sending, ConversationHost re-checks it because it is a public entry point, and
/// the Worker's own resolver executes only what is baked into its image. None of the three relies
/// on the others having run.
/// </summary>
internal static class ProjectMissions
{
    public const string Janus = "Janus";
    public const string Naive = "Naive";

    /// <summary>The mission a Project starts out on, and the one every migrated Project keeps.</summary>
    public const string Default = Janus;

    /// <summary>The catalog in the order a picker shows it, default first. It exists so a surface
    /// renders the two names without holding its own copy of them: this class stays the one place
    /// that knows what the catalog is, and a TUI reads the same list through the same contract
    /// (43.21 task 2).</summary>
    public static IReadOnlyList<string> All { get; } = [Janus, Naive];

    /// <summary>Case-sensitive on purpose: <c>Janus</c> and <c>Naive</c> are names, and accepting
    /// "janus" would mean the persisted value depends on how it was typed.</summary>
    public static bool IsAllowed(string? mission) =>
        mission is Janus or Naive;

    /// <summary>The canonical manifest value for an allowed mission. Both are built-in, so neither
    /// carries a path or a digest — there is nothing here a caller could point at a local file.</summary>
    public static ProjectMissionReference Reference(string mission) =>
        IsAllowed(mission)
            ? new ProjectMissionReference(ProjectMissionOrigin.BuiltIn, mission, null)
            : throw new ArgumentOutOfRangeException(nameof(mission), mission, "Not an allowed Project mission.");

    /// <summary>What a manifest's selection means as a mission ref. A selection that is somehow not
    /// a built-in allowed mission is refused rather than defaulted: silently running Janus because
    /// a hand-edited manifest named something else would execute work nobody selected.</summary>
    public static string RequireSelected(ProjectMissionReference? selected) =>
        selected is { Origin: ProjectMissionOrigin.BuiltIn } builtIn && IsAllowed(builtIn.Reference)
            ? builtIn.Reference
            : throw new ProjectOperationException(
                ProjectOperationErrorCode.UnknownMission,
                $"This Project selects '{selected?.Reference}', which is not a mission Forge can run.");
}
