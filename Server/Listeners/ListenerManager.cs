using System.Collections.Concurrent;

namespace Server.Listeners;

public sealed class ListenerManager
{
    private readonly ConcurrentDictionary<uint, Listener> _listeners = [];

    public void Add(Listener listener)
    {
        _listeners.AddOrUpdate(listener.Id, listener, (_, _) => listener);
    }

    public List<Listener> List()
    {
        return _listeners.Values.ToList();
    }

    public Task Remove(uint id)
    {
        return _listeners.TryRemove(id, out var listener)
            ? listener.Stop()
            : Task.CompletedTask;
    }
}