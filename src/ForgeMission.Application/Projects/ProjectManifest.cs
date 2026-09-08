namespace ForgeMission.Application;

// The local, Forge-owned Project record written to <project-home>/forge.project.json. v5 retains
// earlier fields for read compatibility, adds authored definitions, and keeps the v4 launch lane.
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
    Guid? ProjectMissionContainerId,
    ProjectRunMetadata[] Runs,
    Guid? LegacyProjectControlConversationId = null,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    Guid? MissionControlConversationId = null,
    ProjectSubmission? Submission = null,
    MissionVersionLaunch[]? ApprovedMissionLaunches = null,
    ProjectMissionDefinition[]? MissionDefinitions = null)
{
    public const int CurrentSchemaVersion = 5;
}

// Authored definitions are a Project concern.  They deliberately sit beside, rather than inside,
// the v4 compatibility launch lane: an old launch has no authored identity or evaluation facts.
internal sealed record ProjectMissionDefinition(
    Guid MissionId,
    string Name,
    Guid? ActiveApprovedVersionId,
    MissionDraft? Draft,
    MissionVersion[]? Versions);

internal sealed record MissionDraft(
    Guid DraftId,
    string DefinitionText,
    string DefinitionHash,
    ForgeMission.Conversations.Contracts.MissionHandsProfile CapabilityProfile,
    int Revision,
    DateTimeOffset UpdatedAtUtc);

internal sealed record MissionVersion(
    Guid MissionVersionId,
    int VersionNumber,
    MissionVersionState State,
    string DefinitionText,
    string DefinitionHash,
    ForgeMission.Conversations.Contracts.MissionHandsProfile CapabilityProfile,
    ForgeMission.Conversations.Contracts.DurableMissionPackage Package,
    Guid? ParentVersionId,
    int CandidateRevision,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? EvaluatedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    EvaluationCase[]? EvaluationCases,
    EvaluationResult[]? EvaluationResults);

internal enum MissionVersionState { Candidate, Evaluated, Approved, Superseded }

internal sealed record EvaluationCase(
    Guid EvaluationCaseId,
    string Input,
    string ExpectedSuccess,
    string ExpectedFailure,
    EvaluationOutcome ExpectedOutcome,
    string[]? RequiredOutputFragments,
    string[]? ForbiddenOutputFragments,
    int Revision);

internal sealed record EvaluationResult(
    Guid EvaluationResultId,
    Guid EvaluationCaseId,
    Guid MissionVersionId,
    int CandidateRevision,
    string DefinitionHash,
    EvaluationOutcome? ObservedOutcome,
    string? ObservedOutputSummary,
    EvaluationResultState State,
    EvaluationTraceOrigin? TraceOrigin,
    DateTimeOffset? CompletedAtUtc);

internal enum EvaluationOutcome { Succeeded, Failed }
internal enum EvaluationResultState { Pending, Passed, Failed }
internal sealed record EvaluationTraceOrigin(Guid ConversationId, Guid TurnId, Guid TurnAttemptId);

/// <summary>Project-owned pending intent returned to the Missions coordinator. It is a value
/// snapshot only: it contains no local path, capability authority, or remote state.</summary>
internal sealed record PendingEvaluationAdmission(
    Guid ProjectId,
    Guid MissionId,
    MissionVersion Version,
    EvaluationCase EvaluationCase,
    EvaluationResult Result);

/// <summary>Application-internal provenance assembled from one current Project manifest read.
/// It is an immutable value passed to Missions, never a durable contract and never a Project path.</summary>
internal sealed record MissionLaunchProvenance(Guid ProjectId, Guid MissionId,
    ForgeMission.Conversations.Contracts.DurableMissionLaunch Launch);

internal enum ProjectSubmissionPhase
{
    Prepared,
    Accepted,
    Rejected,
}

internal sealed record ProjectSubmission(
    Guid CommandId,
    Guid? PreviousCommandId,
    string Mission,
    string Input,
    string ProjectGoal,
    ProjectSubmissionPhase Phase,
    ProjectSubmissionAcceptance? Acceptance,
    ProjectSubmissionRejection? Rejection);

internal sealed record ProjectSubmissionAcceptance(
    Guid ContainerId,
    Guid RunId,
    long AcceptedSequence,
    Conversations.Contracts.ConversationRunStatus Status);

internal sealed record ProjectSubmissionRejection(string Code, string Message);

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
        new(ProjectMissionOrigin.BuiltIn, Conversations.Contracts.ProjectMissionNames.Janus, null);
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
