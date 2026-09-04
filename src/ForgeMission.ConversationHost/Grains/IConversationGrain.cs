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

    /// <summary>Creates this conversation as a Project's zero-tool Mission Control conversation
    /// (43.20 task 2), pinning its purpose, Project ID and goal in ONE checkpoint write and
    /// appending NO event — a newly created control conversation is empty, so its accepted
    /// sequence is <c>0</c>. An exact retry (same Project and goal) returns the same answer; a
    /// create naming a different Project or goal, or one against a conversation already pinned to
    /// a Janus run, is a typed <see cref="ConversationCommandOutcome.Conflict"/>.</summary>
    Task<ConversationCommandOutcomeResult> AcceptControlCreateAsync(ConversationControlCreateInput input);

    /// <summary>Records one Project-control turn (43.20 task 2): appends exactly one
    /// <c>UserMessage</c> with a null <c>RunId</c> and dispatches exactly one zero-tool
    /// MissionControl command. It starts no run, allocates no run ID, appends no
    /// <c>RunStatus</c>, and never notifies <c>MissionRunGrain</c>. The dispatched command's
    /// <c>ProjectGoal</c> comes from pinned checkpoint state, never from
    /// <paramref name="input"/>, which has no such member.</summary>
    Task<ConversationCommandOutcomeResult> AcceptControlMessageAsync(ConversationControlMessageInput input);

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
