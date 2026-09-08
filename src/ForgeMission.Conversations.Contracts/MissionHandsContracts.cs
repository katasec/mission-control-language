using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForgeMission.Conversations.Contracts;

/// <summary>Closed durable profile vocabulary.  A caller can acknowledge but cannot select it.</summary>
public enum MissionHandsProfile
{
    [JsonStringEnumMemberName("noHands")] NoHands,
    [JsonStringEnumMemberName("projectWorkspace")] ProjectWorkspace,
    [JsonStringEnumMemberName("projectWorkspaceAndTerminal")] ProjectWorkspaceAndTerminal,
}

/// <summary>Opaque, profile-pinned mission provenance retained by the Conversation owner.</summary>
public sealed record DurableMissionLaunch(
    Guid MissionVersionId,
    int VersionNumber,
    string DefinitionHash,
    string Definition,
    MissionHandsProfile Profile,
    DurableMissionPackage? Package = null);

/// <summary>
/// Immutable value package supplied with a generic durable launch.  It deliberately contains
/// executable declaration and resolved expert content only: provider selection, credentials,
/// filesystem roots and capability handles are not representable here.
/// </summary>
public sealed record DurableMissionPackage(
    int FormatVersion,
    string PackageHash,
    string MissionSource,
    string RootMissionName,
    string RootInputName,
    DurableResolvedExpert[] ResolvedExperts);

/// <summary>One already-resolved expert, retained as bounded immutable content rather than a
/// Worker image path or a registry reference.</summary>
public sealed record DurableResolvedExpert(
    string Name,
    string LockSource,
    string LockPath,
    string LockHash,
    string ExpertMarkdown);

/// <summary>
/// The single owner of "are these the same pinned launch?" — a security-relevant durable rule
/// (create idempotency, attachment-vs-pinned refusal, hands-request correlation), so it has one
/// implementation rather than one per caller.
/// <para>
/// It exists because <see cref="DurableMissionPackage.ResolvedExperts"/> is an array, which the
/// generated record comparer compares by reference: two launches that crossed JSON are never
/// <c>==</c>, even when every value matches. Every pinned launch is stored serialized, so a
/// re-registration always compares a fresh instance against a deserialized one.
/// </para>
/// Comparison is ordinal throughout: these are hashes, identifiers, and exact content, never
/// display text.
/// </summary>
public static class DurableMissionLaunchComparison
{
    /// <summary>Structural equality over every immutable launch field, including definition
    /// content and the whole resolved package. Two nulls are the same launch; one null is not.</summary>
    public static bool SameLaunch(DurableMissionLaunch? left, DurableMissionLaunch? right)
    {
        if (left is null || right is null) return left is null && right is null;
        return left.MissionVersionId == right.MissionVersionId &&
            left.VersionNumber == right.VersionNumber &&
            string.Equals(left.DefinitionHash, right.DefinitionHash, StringComparison.Ordinal) &&
            string.Equals(left.Definition, right.Definition, StringComparison.Ordinal) &&
            left.Profile == right.Profile &&
            SamePackage(left.Package, right.Package);
    }

    /// <summary>Structural equality over the frozen package and every resolved expert. A missing
    /// package matches only another missing package — a schema-4 launch is never the same value
    /// as a package-bearing one.</summary>
    public static bool SamePackage(DurableMissionPackage? left, DurableMissionPackage? right)
    {
        if (left is null || right is null) return left is null && right is null;
        return left.FormatVersion == right.FormatVersion &&
            string.Equals(left.PackageHash, right.PackageHash, StringComparison.Ordinal) &&
            string.Equals(left.MissionSource, right.MissionSource, StringComparison.Ordinal) &&
            string.Equals(left.RootMissionName, right.RootMissionName, StringComparison.Ordinal) &&
            string.Equals(left.RootInputName, right.RootInputName, StringComparison.Ordinal) &&
            left.ResolvedExperts.Length == right.ResolvedExperts.Length &&
            left.ResolvedExperts.Zip(right.ResolvedExperts).All(pair =>
                string.Equals(pair.First.Name, pair.Second.Name, StringComparison.Ordinal) &&
                string.Equals(pair.First.LockSource, pair.Second.LockSource, StringComparison.Ordinal) &&
                string.Equals(pair.First.LockPath, pair.Second.LockPath, StringComparison.Ordinal) &&
                string.Equals(pair.First.LockHash, pair.Second.LockHash, StringComparison.Ordinal) &&
                string.Equals(pair.First.ExpertMarkdown, pair.Second.ExpertMarkdown, StringComparison.Ordinal));
    }
}

public enum MissionHandsStatus
{
    [JsonStringEnumMemberName("attached")] Attached,
    [JsonStringEnumMemberName("awaitingHands")] AwaitingHands,
    [JsonStringEnumMemberName("awaitingToolConfirmation")] AwaitingToolConfirmation,
    [JsonStringEnumMemberName("inFlight")] InFlight,
    [JsonStringEnumMemberName("completed")] Completed,
    [JsonStringEnumMemberName("cancelled")] Cancelled,
    [JsonStringEnumMemberName("interrupted")] Interrupted,
}

public enum MissionToolOutcome
{
    [JsonStringEnumMemberName("succeeded")] Succeeded,
    [JsonStringEnumMemberName("deniedOutOfProfile")] DeniedOutOfProfile,
    [JsonStringEnumMemberName("deniedByPolicy")] DeniedByPolicy,
    [JsonStringEnumMemberName("deniedByOperator")] DeniedByOperator,
    [JsonStringEnumMemberName("cancelled")] Cancelled,
    [JsonStringEnumMemberName("failed")] Failed,
    [JsonStringEnumMemberName("interrupted")] Interrupted,
}

/// <summary>Contains no local root, credential, Bob handle, or Worker transcript.</summary>
public sealed record MissionToolRequest(
    Guid ToolRequestId,
    Guid ConversationId,
    Guid TurnAttemptId,
    string MissionLocation,
    string AgentLocation,
    string ProviderToolCallId,
    string ToolName,
    JsonElement Arguments,
    string OpaqueContinuation);

public sealed record MissionHandsAttachment(
    Guid ConversationId,
    Guid AttachmentId,
    Guid ApplicationSessionId,
    DurableMissionLaunch Launch,
    DateTimeOffset AttachedAtUtc);

public sealed record AttachMissionHandsRequest(
    Guid ConversationId, Guid AttachmentId, Guid ApplicationSessionId, DurableMissionLaunch Launch);
public sealed record DetachMissionHandsRequest(Guid ConversationId, Guid AttachmentId);
public sealed record SubmitMissionToolResultRequest(
    Guid ConversationId, Guid AttachmentId, Guid TurnAttemptId, Guid CommandId, Guid ToolRequestId,
    MissionToolOutcome Outcome, string? Content, string? Reason);
/// <summary>Application can read work only when this exact fresh attachment/session pair is
/// live. The request never travels through Presentation or Application transport.</summary>
public sealed record GetMissionHandsWorkRequest(
    Guid ConversationId, Guid AttachmentId, Guid ApplicationSessionId);
public sealed record ClaimMissionHandsWorkRequest(
    Guid ConversationId, Guid AttachmentId, Guid ApplicationSessionId);
public sealed record MissionHandsWorkItem(
    MissionHandsStatus Status, MissionHandsAttachment? Attachment, MissionToolRequest? Request,
    long? Sequence, string? Reason = null);
public sealed record BeginMissionHandsConfirmationRequest(
    Guid ConversationId, Guid AttachmentId, Guid ToolRequestId);
public sealed record CancelMissionHandsAttemptRequest(
    Guid ConversationId, Guid AttachmentId, Guid TurnAttemptId, Guid ToolRequestId, string Reason);
/// <summary>A fresh pending attachment terminalizes an unknown old in-flight operation. Host
/// derives the old request and fixed reason; callers cannot select a result or continuation.</summary>
public sealed record RecoverMissionHandsInFlightRequest(
    Guid ConversationId, Guid AttachmentId, Guid ApplicationSessionId);
public sealed record MissionHandsResult(
    MissionHandsStatus Status, long? AcceptedSequence, string? Reason = null);
