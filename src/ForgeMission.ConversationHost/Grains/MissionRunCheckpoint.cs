using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Grains;

/// <summary>Where a run's execution stands relative to a completed safe boundary. Never
/// <c>ExecutingProvider</c> after a repaired activation — an uncertain in-flight provider call
/// becomes <c>Terminal</c>/<c>Interrupted</c>, never silently replayed.</summary>
public enum MissionRunExecutionBoundary
{
    NotStarted,
    ExecutingProvider,
    WaitingForTool,
    Terminal,
}

/// <summary>
/// <c>MissionRunGrain</c>'s Orleans persistent state. <see cref="InterruptionEventId"/>/
/// <see cref="InterruptionOccurredAtUtc"/> are null until an activation needs to report an
/// interruption, then stay stable across retries so the report to <c>ConversationGrain</c> is
/// idempotent by event ID. A mutable class — imperatively updated in place, never compared by value.
/// </summary>
[GenerateSerializer]
public sealed class MissionRunCheckpoint
{
    [Id(0)] public string TenantId { get; set; } = "";
    [Id(1)] public Guid RunId { get; set; }
    [Id(2)] public Guid ConversationId { get; set; }
    [Id(3)] public ConversationRunStatus Status { get; set; }
    [Id(4)] public MissionRunExecutionBoundary ExecutionBoundary { get; set; }
    [Id(5)] public Guid? InterruptionEventId { get; set; }
    [Id(6)] public DateTimeOffset? InterruptionOccurredAtUtc { get; set; }
    [Id(7)] public DateTimeOffset UpdatedAtUtc { get; set; }
}
