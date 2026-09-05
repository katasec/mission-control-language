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

    /// <summary>The full accepted StartMission <see cref="ConversationCommand"/> JSON for the
    /// active run — set once on acceptance, retained until that run reaches a terminal
    /// <see cref="ConversationRunStatus"/>, so a matching ToolResult can derive its
    /// ContinueAfterTool continuation without querying another store.</summary>
    [Id(9)] public string? ActiveStartCommandJson { get; set; }

    /// <summary>The validated <see cref="ConversationCapabilityDeclaration"/> array JSON pinned on
    /// the conversation's first accepted start — retained through every later follow-up run
    /// (including after a run reaches a terminal status, unlike <see cref="ActiveStartCommandJson"/>)
    /// so a follow-up command can never let an adapter select different capabilities.</summary>
    [Id(10)] public string? PinnedCapabilitiesJson { get; set; }

    /// <summary>Non-null while a start pair (<c>UserMessage</c> + paired <c>RunStatus(Queued)</c>)
    /// is in flight for the current run — see the <see cref="PendingRunStart"/> record.</summary>
    [Id(11)] public PendingRunStart? PendingRunStart { get; set; }

    /// <summary>What this conversation is for (43.20 task 2). Appended as a NEW Orleans ID rather
    /// than renumbering: a checkpoint persisted before this field existed reads it as
    /// <see cref="ConversationPurpose.MissionRun"/> (ordinal 0), which is exactly what such a
    /// conversation is — so no migration is needed and no existing conversation can silently
    /// acquire control semantics.</summary>
    [Id(12)] public ConversationPurpose Purpose { get; set; }

    /// <summary>The Project associated with a Project Mission container or a historical Project
    /// Control record. Project Control fields remain only so existing checkpoint rows deserialize;
    /// they are never written by the active product.</summary>
    [Id(13)] public Guid? ProjectId { get; set; }

    /// <summary>The pinned Project goal. Historic Project Control rows retain it for readback;
    /// active Project Mission containers use it when starting their child runs.</summary>
    [Id(14)] public string? ProjectGoal { get; set; }
}
