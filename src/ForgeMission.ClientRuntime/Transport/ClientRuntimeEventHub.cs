using System.Collections.Concurrent;
using System.Threading.Channels;
using ForgeMission.ClientRuntime.Transport;

namespace ForgeMission.ClientRuntime.TransportHost;

internal sealed class ClientRuntimeEventHub
{
    private readonly ConcurrentDictionary<Guid, Channel<ClientRuntimeEvent>> _subscribers = [];

    public void Publish(ClientRuntimeEvent message)
    {
        foreach (var subscriber in _subscribers.Values)
            subscriber.Writer.TryWrite(message);
    }

    public async IAsyncEnumerable<ClientRuntimeEvent> Subscribe(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<ClientRuntimeEvent>();
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
