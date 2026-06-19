using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Server.Core;

public sealed class CrystalBroker<T> : IBroker<T> where T : class
{
    private readonly ConcurrentDictionary<Guid, Channel<T>> _subscribers = new();

    public void Publish(T obj)
    {
        // write to every channel so it can be broadcast
        // to all clients
        foreach (var (_, channel) in _subscribers)
            channel.Writer.TryWrite(obj);
    }

    public (ChannelReader<T> Reader, IDisposable Subscription) Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _subscribers[id] = channel;

        var subscription = new Subscription(() =>
        {
            if (_subscribers.TryRemove(id, out var ch))
                ch.Writer.TryComplete();
        });

        return (channel.Reader, subscription);
    }

    private sealed class Subscription(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}