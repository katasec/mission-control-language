using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Presentation;

/// <summary>
/// The one presentation-only wording for a mission's fixed access profile. Two surfaces state the
/// same fixed access, so they read the same words from here rather than each holding a copy. It
/// renders text and decides nothing: the profile itself is fixed by the approved version, and Client
/// Runtime alone authorizes what it permits.
/// </summary>
internal static class MissionProfileLabel
{
    internal static string For(MissionHandsProfile profile) => profile switch
    {
        MissionHandsProfile.NoHands => "No local access",
        MissionHandsProfile.ProjectWorkspace => "Project workspace access",
        MissionHandsProfile.ProjectWorkspaceAndTerminal => "Project workspace and terminal access",
        _ => "Unknown access",
    };
}
