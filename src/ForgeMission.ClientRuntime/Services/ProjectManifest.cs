namespace ForgeMission.ClientRuntime.Services;

// The local, Forge-owned Project record written to <project-home>/forge.project.json (43.20 task 1).
// The complete v1 shape is defined here even though Task 1 only ever writes empty collections and a
// null conversation ID: later tasks add facts to typed empty arrays instead of silently changing the
// on-disk schema, and Task 2's conversation-ID write-back round-trips the whole graph.
//
// It holds no credential, secret-derived value, transcript, or remote connection string. Absolute
// local paths (a context SourceRoot/File reference) stay in this file; they never cross the
// Conversation boundary.
internal sealed record ProjectManifest(
    int SchemaVersion,
    Guid ProjectId,
    string Title,
    string Goal,
    ProjectAssetDescriptor[] Assets,
    ProjectMissionReference SelectedMission,
    ProjectContextDescriptor[] AttachedContext,
    Guid? MissionControlConversationId,
    ProjectRunMetadata[] Runs)
{
    public const int CurrentSchemaVersion = 1;
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
