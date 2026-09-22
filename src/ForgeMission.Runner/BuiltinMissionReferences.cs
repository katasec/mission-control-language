using ForgeMission.Core.Resolution;

namespace ForgeMission.Runner;

/// <summary>Digest-pinned default mission reference owned by the Runner's baked fallback.</summary>
internal static partial class BuiltinMissionReferences
{
    private const string VanillaResource = "ForgeMission.Runner.BuiltinMissionReferences.vanilla.oci-ref";

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
