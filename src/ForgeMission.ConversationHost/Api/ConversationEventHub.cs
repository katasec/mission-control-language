using System.Collections.Concurrent;
using System.Threading.Channels;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Api;

/// <summary>
/// The one in-process singleton implementation of <see cref="IConversationEventNotifier"/> (Task 6).
/// Keeps the grain independent of the HTTP adapter — it is not transcript storage and not an
/// Orleans Stream. Each subscription is scoped to one <see cref="ConversationAddress"/> and backed
/// by a fixed bounded channel of 64 events; <see cref="Publish"/> never awaits a slow client. If a
/// channel cannot accept an event, that subscription is marked stale, completed, and removed — the
/// SSE endpoint then ends normally and the client reconnects from its last rendered sequence via
/// Table replay. One Silo/one Host replica is an explicit Task 6 limitation; a future
/// multi-replica deployment replaces only this notifier/backplane, not the replay contract.
/// </summary>
public sealed class ConversationEventHub : IConversationEventNotifier
{
    private const int ChannelCapacity = 64;

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Subscription, byte>> _subscriptionsByAddress = new();

    public IConversationEventSubscription Subscribe(ConversationAddress address)
    {
        var subscription = new Subscription(this, address.PartitionKey);
        var bucket = _subscriptionsByAddress.GetOrAdd(address.PartitionKey, static _ => new ConcurrentDictionary<Subscription, byte>());
        bucket[subscription] = 0;
        return subscription;
    }

    public void Publish(ConversationAddress address, ConversationEvent @event)
    {
        if (!_subscriptionsByAddress.TryGetValue(address.PartitionKey, out var bucket))
            return;

        foreach (var subscription in bucket.Keys)
        {
            if (!subscription.Writer.TryWrite(@event))
                Remove(subscription); // Full/closed channel: mark stale and drop it, never block.
        }
    }

    private void Remove(Subscription subscription)
    {
        subscription.Writer.TryComplete();
        if (_subscriptionsByAddress.TryGetValue(subscription.PartitionKey, out var bucket))
            bucket.TryRemove(subscription, out _);
    }

    private sealed class Subscription : IConversationEventSubscription
    {
        private readonly ConversationEventHub _hub;
        private readonly Channel<ConversationEvent> _channel = Channel.CreateBounded<ConversationEvent>(
            new BoundedChannelOptions(ChannelCapacity) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = false });

        public Subscription(ConversationEventHub hub, string partitionKey)
        {
            _hub = hub;
            PartitionKey = partitionKey;
        }

        public string PartitionKey { get; }

        public ChannelWriter<ConversationEvent> Writer => _channel.Writer;

        public ChannelReader<ConversationEvent> Reader => _channel.Reader;

        public ValueTask DisposeAsync()
        {
            _hub.Remove(this);
            return ValueTask.CompletedTask;
        }
    }
}
