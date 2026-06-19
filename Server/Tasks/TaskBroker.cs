using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Server.Tasks;

/// <summary>
/// Singleton broker that routes beacon task output to the specific client
/// that submitted the original TaskRequest, identified by task ID.
/// </summary>
public sealed class TaskBroker
{
    private readonly ConcurrentDictionary<uint, ChannelWriter<BeaconCallback>> _writers = new();

    /// <summary>
    /// Routes output to the writer registered for the output's task ID.
    /// Silently drops output for unknown or already-unregistered task IDs.
    /// </summary>
    public void Publish(BeaconCallback output)
    {
        if (_writers.TryGetValue(output.TaskId, out var writer))
        {
            writer.TryWrite(output);
        }
    }

    /// <summary>
    /// Registers a task ID to route its output to the given client channel writer.
    /// Dispose the returned handle to unregister (e.g. when the client disconnects).
    /// </summary>
    public IDisposable Register(uint taskId, ChannelWriter<BeaconCallback> writer)
    {
        _writers[taskId] = writer;
        return new Registration(() => _writers.TryRemove(taskId, out _));
    }

    private sealed class Registration(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}