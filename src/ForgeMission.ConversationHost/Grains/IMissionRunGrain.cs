using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Grains;

/// <summary>
/// The run state machine and terminal-state owner for one run, keyed <c>{TenantId}|{RunId:N}</c>.
/// Never creates a per-expert grain. Internal to ConversationHost.
/// </summary>
public interface IMissionRunGrain : Orleans.IGrainWithStringKey
{
    /// <summary>Called by <see cref="ConversationGrain"/> only after the corresponding event has
    /// durably appended (except for the dedicated interruption-report path, which never calls
    /// this).</summary>
    Task ApplyDurableEventAsync(MissionRunEventInput @event);

    Task<ConversationRunStatus> GetStatusAsync();
}
