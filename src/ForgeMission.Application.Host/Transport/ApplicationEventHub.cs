using System.Collections.Concurrent;
using System.Threading.Channels;
using ForgeMission.Application.Transport;

namespace ForgeMission.Application.Host;

internal sealed class ApplicationEventHub
{
    private readonly ConcurrentDictionary<Guid, Channel<ApplicationEvent>> _subscribers = [];

    public void Publish(ApplicationEvent message)
    {
        foreach (var subscriber in _subscribers.Values)
            subscriber.Writer.TryWrite(message);
    }

    public async IAsyncEnumerable<ApplicationEvent> Subscribe(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<ApplicationEvent>();
        _subscribers[id] = channel;
        try
        {
            await foreach (var message in channel.Reader.ReadAllAsync(ct))
                yield return message;
        }
        finally
        {
            _subscribers.TryRemove(id, out _);
        }
    }
}
