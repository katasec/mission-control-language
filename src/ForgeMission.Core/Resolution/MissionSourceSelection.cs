using System.Text.RegularExpressions;

namespace ForgeMission.Core.Resolution;

// Pure startup contract shared by Mission Runtime hosts. It decides source ownership without
// knowing how a particular host fetches or loads the selected mission.
public sealed partial record MissionSourceSelection(string? MissionRef, string? MissionFile)
{
    public bool UsesOci => MissionRef is not null;
    public bool UsesFile => MissionFile is not null;

    public static MissionSourceSelection Create(string? missionRef, string? missionFile)
    {
        if (!string.IsNullOrWhiteSpace(missionRef) && !string.IsNullOrWhiteSpace(missionFile))
            throw new InvalidOperationException("Set either MissionRef or MissionFile, not both.");

        if (!string.IsNullOrWhiteSpace(missionRef))
        {
            ValidateMissionRef(missionRef);
            return new MissionSourceSelection(missionRef, null);
        }

        return new MissionSourceSelection(null, string.IsNullOrWhiteSpace(missionFile) ? null : missionFile);
    }

    public static void ValidateMissionRef(string missionRef)
    {
        if (!PinnedMissionRef().IsMatch(missionRef))
        {
            throw new InvalidOperationException(
                "MissionRef must be a digest-pinned OCI reference such as " +
                "ghcr.io/katasec/forge-mission-vanilla@sha256:<64 lowercase hex characters>.");
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]*(?::[0-9]+)?/[a-z0-9][a-z0-9._/-]*@sha256:[a-f0-9]{64}$")]
    private static partial Regex PinnedMissionRef();
}
