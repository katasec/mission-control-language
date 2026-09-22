using System.Text.RegularExpressions;

namespace ForgeMission.Orchestration;

/// <summary>Built-in default for the Docker Mission Runtime when no MissionRuntime:Docker:MissionRef
/// is configured — same embedded, digest-pinned reference ForgeMission.Cli uses, so local orchestration
/// works out of the box with zero configuration.</summary>
public static partial class BuiltinMissionReferences
{
    private const string VanillaResource = "ForgeMission.Orchestration.BuiltinMissionReferences.vanilla.oci-ref";

    public static string Vanilla { get; } = ReadVanilla();

    private static string ReadVanilla()
    {
        using var stream = typeof(BuiltinMissionReferences).Assembly.GetManifestResourceStream(VanillaResource)
            ?? throw new InvalidOperationException($"Missing embedded built-in mission reference '{VanillaResource}'.");
        using var reader = new StreamReader(stream);
        var missionRef = reader.ReadToEnd().Trim();
        if (!PinnedMissionRef().IsMatch(missionRef))
        {
            throw new InvalidOperationException(
                "MissionRef must be a digest-pinned OCI reference such as " +
                "ghcr.io/katasec/forge-mission-vanilla@sha256:<64 lowercase hex characters>.");
        }

        return missionRef;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]*(?::[0-9]+)?/[a-z0-9][a-z0-9._/-]*@sha256:[a-f0-9]{64}$")]
    private static partial Regex PinnedMissionRef();
}
