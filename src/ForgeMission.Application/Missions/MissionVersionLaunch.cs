namespace ForgeMission.Application;

/// <summary>
/// Immutable, locally validated provenance for a version that is already approved.  Application
/// is the sole creator/reader of this record; callers acknowledge this exact launch but cannot
/// supply a profile, definition, or attachment authority of their own.
/// </summary>
internal sealed record MissionVersionLaunch(
    Guid MissionVersionId,
    int VersionNumber,
    string DefinitionHash,
    string Definition,
    MissionCapabilityProfile CapabilityProfile,
    DateTimeOffset ApprovedAtUtc,
    ForgeMission.Conversations.Contracts.DurableMissionPackage? Package = null);
