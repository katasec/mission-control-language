namespace ForgeMission.ClientRuntime.Services;

// The local, Forge-owned Project record written to <project-home>/forge.project.json (43.20 task 1,
// schema v2 by 43.21 task 1). The complete shape is defined here even though a new Project is
// written with empty collections and a null container ID: later tasks add facts to typed empty
// arrays instead of silently changing the on-disk schema.
//
// It holds no credential, secret-derived value, transcript, or remote connection string. Absolute
// local paths (a context SourceRoot/File reference) stay in this file; they never cross the
// Conversation boundary.
/// <param name="ProjectMissionContainerId">The Project's one durable Mission container — the
/// ConversationHost conversation that orders and replays its child Mission Runs. Null until the
/// first time the workbench opens it. It executes nothing itself.</param>
/// <param name="LegacyProjectControlConversationId">A Project's former Mission Control
/// conversation. Migration moves a v1 value here, and until 43.21 task 3 removes the legacy route
/// the still-compiling ProjectControl session also records one here so an existing Desktop keeps
/// working during that window. After task 3 it is a read-only pointer to durable history: those
/// messages are never replayed as a current mission and never converted into runs.</param>
/// <param name="MissionControlConversationId">v1 ONLY. It exists solely so a v1 file's value can
/// be read and moved into <paramref name="LegacyProjectControlConversationId"/>; migration then
/// nulls it, and being null it is omitted on write — so a v2 file never contains the old key. It
/// is deliberately the one member with a WhenWritingNull condition, against this manifest's
/// otherwise-explicit-nulls policy, because an absent key is exactly what "not v1" means.</param>
internal sealed record ProjectManifest(
    int SchemaVersion,
    Guid ProjectId,
    string Title,
    string Goal,
    ProjectAssetDescriptor[] Assets,
    ProjectMissionReference SelectedMission,
    ProjectContextDescriptor[] AttachedContext,
    Guid? ProjectMissionContainerId,
    ProjectRunMetadata[] Runs,
    Guid? LegacyProjectControlConversationId = null,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    Guid? MissionControlConversationId = null)
{
    /// <summary>v2 (43.21 task 1): <c>missionControlConversationId</c> became
    /// <see cref="ProjectMissionContainerId"/>, and a migrated Project keeps its old control
    /// conversation in <see cref="LegacyProjectControlConversationId"/>.</summary>
    public const int CurrentSchemaVersion = 2;
}

/// <summary>An editable local Forge asset. <paramref name="RelativePath"/> is normalized,
/// home-relative, and never escapes the Project home.</summary>
internal sealed record ProjectAssetDescriptor(
    ProjectAssetKind Kind,
    string RelativePath,
    string? ContentHash);

internal enum ProjectAssetKind
{
    Mission,
    Expert,
    LockFile,
}

/// <summary>The Project's mutable mission selection. A local reference is home-relative and an OCI
/// reference carries a pinned <paramref name="Digest"/>; the local content hash deliberately lives
/// in an immutable run snapshot rather than here.</summary>
internal sealed record ProjectMissionReference(
    ProjectMissionOrigin Origin,
    string Reference,
    string? Digest)
{
    public static ProjectMissionReference BuiltInJanus { get; } =
        new(ProjectMissionOrigin.BuiltIn, "Janus", null);
}

internal enum ProjectMissionOrigin
{
    BuiltIn,
    Local,
    Oci,
}

/// <summary><paramref name="Reference"/> is an absolute local path for <see cref="ProjectContextKind.SourceRoot"/>
/// and <see cref="ProjectContextKind.File"/>, and an opaque artifact ID for
/// <see cref="ProjectContextKind.Artifact"/>.</summary>
internal sealed record ProjectContextDescriptor(
    string Id,
    ProjectContextKind Kind,
    string DisplayName,
    string Reference,
    string? ContentHash);

internal enum ProjectContextKind
{
    SourceRoot,
    File,
    Artifact,
}

/// <summary>Local projection of one named run. The durable Conversation context remains canonical
/// for its events and status — <paramref name="Status"/> is that shared lifecycle, never a local
/// parallel enum.</summary>
internal sealed record ProjectRunMetadata(
    Guid RunId,
    string Title,
    Conversations.Contracts.ConversationRunStatus Status,
    Guid? PredecessorRunId,
    ProjectLaunchSnapshot LaunchSnapshot);

/// <summary>Immutable launch provenance, written once by Task 4. A later asset, mission, or context
/// edit never changes an existing snapshot, and populating it never crawls a workspace.</summary>
internal sealed record ProjectLaunchSnapshot(
    ProjectMissionReference Mission,
    string? LocalMissionContentHash,
    ResolvedExpertReference[] ResolvedExperts,
    ProjectContextSnapshot[] Context,
    string? GitRevision,
    ProjectArtifactSnapshot[] Artifacts);

internal sealed record ResolvedExpertReference(string Reference, string Digest);

internal sealed record ProjectContextSnapshot(string ContextId, string? ContentHash);

internal sealed record ProjectArtifactSnapshot(string ArtifactId, string? ContentHash);
