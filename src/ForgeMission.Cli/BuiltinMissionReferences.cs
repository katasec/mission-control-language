using ForgeMission.Core.Resolution;

namespace ForgeMission.Cli;

/// <summary>Immutable OCI references for built-in missions. The source file is embedded so an
/// installed CLI has the same value as repository launch scripts without a runtime file dependency.</summary>
public static class BuiltinMissionReferences
{
    private const string VanillaResource = "ForgeMission.Cli.BuiltinMissionReferences.vanilla.oci-ref";

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
