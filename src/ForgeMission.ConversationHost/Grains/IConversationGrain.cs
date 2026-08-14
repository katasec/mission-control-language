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

    Task<ConversationProgressAcceptance> RecordProgressAsync(ConversationProgressInput progress);

    /// <summary>Appends the matching deterministic <c>RunStatus(Interrupted)</c> fact through the
    /// normal pending-transition protocol. Deliberately never calls
    /// <see cref="IMissionRunGrain.ApplyDurableEventAsync"/> back — that grain already persisted
    /// its own Interrupted/Terminal state before calling this, so no activation re-entrancy cycle
    /// is possible.</summary>
    Task RecordRunInterruptionAsync(MissionRunInterruption interruption);

    Task<ConversationSnapshotResult> GetSnapshotAsync();

    Task<ConversationEventBatch> ReadAfterAsync(long sequence);
}
