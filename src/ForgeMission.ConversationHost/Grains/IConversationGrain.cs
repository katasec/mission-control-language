namespace ForgeMission.ConversationHost.Grains;

/// <summary>
/// The sole allocator of conversation event sequence and appender to <c>forgeconversationevents</c>
/// for its <see cref="ConversationAddress"/> (its string grain key). Internal to ConversationHost —
/// later API adapters/queue consumers reach it only through <c>IGrainFactory</c>, never direct
/// storage. Result/acceptance records are internal Host DTOs, not public wire contracts.
/// </summary>
public interface IConversationGrain : Orleans.IGrainWithStringKey
{
    Task<ConversationCommandAcceptance> AcceptCommandAsync(ConversationCommandInput command);

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
