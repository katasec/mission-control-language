using ForgeMission.Core.Resolution;

namespace ForgeMission.ClientRuntime;

/// <summary>Built-in default for the Docker Mission Runtime when no MissionRuntime:Docker:MissionRef
/// is configured — same embedded, digest-pinned reference ForgeMission.Cli uses, so the desktop app
/// works out of the box with zero configuration.</summary>
public static class BuiltinMissionReferences
{
    private const string VanillaResource = "ForgeMission.ClientRuntime.BuiltinMissionReferences.vanilla.oci-ref";

    public static string Vanilla { get; } = ReadVanilla();

    private static string ReadVanilla()
    {
        using var stream = typeof(BuiltinMissionReferences).Assembly.GetManifestResourceStream(VanillaResource)
            ?? throw new InvalidOperationException($"Missing embedded built-in mission reference '{VanillaResource}'.");
        using var reader = new StreamReader(stream);
        var missionRef = reader.ReadToEnd().Trim();
        MissionSourceSelection.ValidateMissionRef(missionRef);
        return missionRef;
    }
}
