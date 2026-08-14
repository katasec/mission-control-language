using System.Threading.Channels;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Grains;

/// <summary>
/// Narrow, grain-owned, post-durability live-notification seam (Task 6). This is <b>not</b>
/// transcript storage and <b>not</b> an Orleans Stream — <c>ConversationGrain</c> publishes only
/// after <c>AppendAsync</c> has succeeded and <c>AdvanceAsync</c> has durably written the
/// checkpoint; notifier failure or an absent subscriber must never change the grain transition, its
/// outbox, or a Service Bus acknowledgement. The SSE writer is the only consumer.
/// </summary>
public interface IConversationEventNotifier
{
    IConversationEventSubscription Subscribe(ConversationAddress address);

    /// <summary>Must never await a slow or absent subscriber — publishing is fire-and-forget from
    /// the grain's perspective. A client can observe a duplicated live notification during grain
    /// recovery; its event ID/sequence makes that harmless.</summary>
    void Publish(ConversationAddress address, ConversationEvent @event);
}

/// <summary>One live subscription to one conversation's post-durability event stream, backed by a
/// fixed bounded channel. A full channel marks the subscription stale and completes it rather than
/// blocking the publisher or growing unbounded.</summary>
public interface IConversationEventSubscription : IAsyncDisposable
{
    ChannelReader<ConversationEvent> Reader { get; }
}
