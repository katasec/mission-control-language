using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Grains;

/// <summary>
/// <c>ConversationGrain</c>'s Orleans persistent state — a compact operational checkpoint, not a
/// growing history. <see cref="Status"/> is a Contracts <em>enum</em> (no <c>JsonElement</c> risk),
/// so it may live directly in Orleans state; <see cref="PendingTransition"/> instead carries a
/// planned event as source-generated JSON text — never a live Contracts record object graph. A
/// mutable class (not a record): this is imperatively updated in place across grain calls, never
/// compared by value.
/// </summary>
[GenerateSerializer]
public sealed class ConversationCheckpoint
{
    [Id(0)] public string TenantId { get; set; } = "";
    [Id(1)] public Guid ConversationId { get; set; }
    [Id(2)] public string MissionRef { get; set; } = "";
    [Id(3)] public Guid? ActiveRunId { get; set; }
    [Id(4)] public long LastSequence { get; set; }
    [Id(5)] public ConversationRunStatus Status { get; set; }
    [Id(6)] public Guid? ExpectedToolRequestId { get; set; }
    [Id(7)] public PendingConversationTransition? PendingTransition { get; set; }
    [Id(8)] public DateTimeOffset UpdatedAtUtc { get; set; }
}
