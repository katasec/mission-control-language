namespace ForgeMission.ConversationHost.Grains;

/// <summary>
/// The sole allocator of conversation event sequence and appender to <c>forgeconversationevents</c>
/// for its <see cref="ConversationAddress"/> (its string grain key). Internal to ConversationHost —
/// later API adapters/queue consumers reach it only through <c>IGrainFactory</c>, never direct
/// storage. Result/acceptance records are internal Host DTOs, not public wire contracts.
/// </summary>
public interface IConversationGrain : Orleans.IGrainWithStringKey
{
    /// <summary>Starts a new conversation (its first-ever run) or, for an exact retry of an
    /// already-accepted <c>CommandId</c>, returns that original acceptance. An active run already
    /// existing, or the same <c>CommandId</c> reused with different content, is a typed
    /// <see cref="ConversationCommandOutcome.Conflict"/> — never a thrown exception.</summary>
    Task<ConversationCommandOutcomeResult> AcceptCommandAsync(ConversationCommandInput command);

    /// <summary>Starts a new run on an existing conversation's pinned mission/capabilities (Task 6).
    /// Same conflict/duplicate semantics as <see cref="AcceptCommandAsync"/>.</summary>
    Task<ConversationCommandOutcomeResult> AcceptFollowupCommandAsync(ConversationFollowupCommandInput input);

    /// <summary>Records a client-submitted tool result for the active run's outstanding tool
    /// request (Task 6). Same conflict/duplicate semantics as <see cref="AcceptCommandAsync"/>.</summary>
    Task<ConversationCommandOutcomeResult> AcceptToolResultAsync(ConversationToolResultInput input);

    /// <summary>Creates this conversation as a Project's Mission container (43.21 task 1), pinning
    /// its purpose, Project ID and goal in ONE checkpoint write and appending NO event, so a newly
    /// created container's accepted sequence is <c>0</c>. Unlike a control conversation it pins NO
    /// mission and NO capabilities — each child run carries its own — so
    /// <see cref="ConversationPurpose.ProjectMission"/> plus a non-null Project ID is what makes it
    /// exist. An exact retry (same Project and goal) returns the same answer; a create naming a
    /// different Project or goal, or one against a conversation already pinned to another purpose,
    /// is a typed <see cref="ConversationCommandOutcome.Conflict"/>.</summary>
    Task<ConversationCommandOutcomeResult> AcceptProjectMissionContainerCreateAsync(ConversationProjectMissionCreateInput input);

    /// <summary>Starts one child Mission Run under this Project's Mission container (43.21 task 1).
    /// The mission and capabilities come from <paramref name="input"/> rather than from pinned
    /// state, which is what lets one Project alternate between Janus and Naive; everything else —
    /// run identity, the UserMessage/RunStatus(Queued) pair, the MissionRunGrain notification, the
    /// outbox dispatch — goes through the SAME <c>BeginRunAsync</c> a Janus start uses, so both
    /// missions produce an identically shaped run. An equal retry returns the original run; the
    /// same command ID with a different mission or input is a
    /// <see cref="ConversationCommandOutcome.Conflict"/>; a second submission while a run is still
    /// active is <see cref="ConversationCommandOutcome.RunAlreadyActive"/> and appends nothing.</summary>
    Task<ConversationCommandOutcomeResult> AcceptProjectMissionRunAsync(ConversationProjectMissionRunInput input);

    Task<ConversationProgressAcceptance> RecordProgressAsync(ConversationProgressInput progress);

    /// <summary>Appends the matching deterministic <c>RunStatus(Interrupted)</c> fact through the
    /// normal pending-transition protocol. Deliberately never calls
    /// <see cref="IMissionRunGrain.ApplyDurableEventAsync"/> back — that grain already persisted
    /// its own Interrupted/Terminal state before calling this, so no activation re-entrancy cycle
    /// is possible.</summary>
    Task RecordRunInterruptionAsync(MissionRunInterruption interruption);

    Task<ConversationSnapshotResult> GetSnapshotAsync();

    Task<ConversationEventBatch> ReadAfterAsync(long sequence);

    Task<ConversationProjectReadResult> ReadProjectRunsAsync(long? anchor, long? before);
    Task<ConversationProjectReadResult> ReadProjectRunAsync(Guid runId);
    Task<ConversationProjectReadResult> ReadProjectRunEventsAsync(Guid runId, long after, long? through);
    Task<ConversationProjectReadResult> ReadProjectCommandAsync(Guid commandId);
}
