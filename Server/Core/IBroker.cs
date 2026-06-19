using System.Threading.Channels;

namespace Server.Core;

public interface IBroker<T> where T : class
{
    void Publish(T obj);
    (ChannelReader<T> Reader, IDisposable Subscription) Subscribe();
}