using ForgeMission.ClientRuntime.Transport;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ClientRuntime.Services;

/// <summary>Validates the fixed Project mission catalog at the local manifest boundary.</summary>
internal static class ProjectMissions
{
    public static IReadOnlyList<string> All => ProjectMissionNames.All;

    public static bool IsAllowed(string? mission) => ProjectMissionNames.IsKnown(mission);

    public static ProjectMissionReference Reference(string mission) =>
        IsAllowed(mission)
            ? new ProjectMissionReference(ProjectMissionOrigin.BuiltIn, mission, null)
            : throw new ArgumentOutOfRangeException(nameof(mission), mission, "Not an allowed Project mission.");

    public static string RequireSelected(ProjectMissionReference? selected) =>
        selected is { Origin: ProjectMissionOrigin.BuiltIn } builtIn && IsAllowed(builtIn.Reference)
            ? builtIn.Reference
            : throw new ProjectOperationException(
                ProjectOperationErrorCode.UnknownMission,
                $"This Project selects '{selected?.Reference}', which is not a mission Forge can run.");
}
