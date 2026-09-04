using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForgeMission.ConversationWorker.Janus;

/// <summary>Where a Service Bus session currently stands. <see cref="ExecutingProvider"/> is
/// persisted before any provider invocation; a redelivery found in this phase means the provider
/// call's outcome is unknown, so it is reported Interrupted rather than replayed.
/// <see cref="WaitingForTool"/> means the one supported tool request is outstanding.
/// <see cref="Terminal"/> means the run reached Completed/Failed and no further command for it is
/// expected.</summary>
public enum WorkerSessionPhase
{
    ExecutingProvider,
    WaitingForTool,
    Terminal,
}

/// <summary>The one outstanding tool call a <see cref="WorkerSessionPhase.WaitingForTool"/> session
/// is waiting on. <see cref="RequestId"/> is the Forge-level correlation ID
/// (<c>ConversationDeterministicIds.ToolRequest</c>) matched against a later
/// <c>ConversationToolResult.RequestId</c>; <see cref="ProviderCallId"/> is the provider's own
/// tool-call ID; <see cref="ToolName"/>/<see cref="Arguments"/> are retained so the continuation
/// can reconstruct the identical <c>FunctionCallContent</c> the provider originally emitted,
/// paired with the new <c>FunctionResultContent</c>.</summary>
public sealed record OutstandingToolCall(Guid RequestId, string ProviderCallId, string ToolName, JsonElement Arguments);

/// <summary>
/// Service Bus session state for one conversation's mission-command session — recovery metadata
/// only, never a transcript, credential, workspace path, or conversation-store data.
/// <see cref="PendingProgressJson"/> is at most one serialized <c>ConversationProgress</c> fact
/// (the outbox pattern applied to a single in-flight fact): persisted before it is sent, cleared
/// only after the publisher accepts it, so a restart resends the identical fact under its already
/// -assigned deterministic ID rather than skipping or duplicating it.
/// </summary>
/// <param name="RunId">Non-null for a Janus mission-run session; null for a Project-control
/// session, which has no run (43.20 task 2). Widening this to nullable is what lets one session
/// record serve both without a second state shape. A session persisted before this change always
/// carries a non-null value and deserializes unchanged — there is no migration and no version
/// field, because the source-generated context reads an absent-or-present GUID into
/// <c>Guid?</c> either way.</param>
public sealed record WorkerSessionState(
    Guid CurrentCommandId,
    Guid? RunId,
    WorkerSessionPhase Phase,
    int NextProgressOrdinal,
    string? PendingProgressJson,
    string? ApprovedPlan,
    OutstandingToolCall? OutstandingTool);

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(WorkerSessionState))]
internal partial class WorkerSessionStateJsonContext : JsonSerializerContext;
