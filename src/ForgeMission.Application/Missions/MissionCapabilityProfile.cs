namespace ForgeMission.Application;

/// <summary>
/// The closed capability declaration pinned to one approved mission version.  This is a version
/// property, not a caller-selected tool list: changing it requires a new approved launch.
/// </summary>
internal enum MissionCapabilityProfile
{
    NoHands,
    ProjectWorkspace,
    ProjectWorkspaceAndTerminal,
}
