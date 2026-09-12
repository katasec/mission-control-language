namespace ForgeMission.Application;

/// <summary>
/// The friendly two-word title the managed chat Project is created with (Phase 48). It is ordinary
/// Project display data, generated once at provisioning from two fixed word lists, and renameable
/// later by a separately designed action. No model is called and no name field is ever shown.
/// </summary>
internal static class ManagedChatProjectNames
{
    private static readonly string[] Qualities =
        ["Amber", "Brisk", "Calm", "Clear", "Bright", "Quiet", "Steady", "Swift", "Warm", "Open"];

    private static readonly string[] Places =
        ["Harbor", "Meadow", "Summit", "Orchard", "Lantern", "Compass", "Garden", "Beacon", "Atrium", "Bridge"];

    internal static string NextTitle() =>
        $"{Pick(Qualities)} {Pick(Places)}";

    private static string Pick(string[] values) => values[System.Random.Shared.Next(values.Length)];
}
