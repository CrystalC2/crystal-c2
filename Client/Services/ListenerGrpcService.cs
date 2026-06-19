using System.Threading;
using System.Threading.Tasks;
using CrystalC2.Listeners;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Client.Services;

internal sealed class ListenerGrpcService(ServerConnection connection)
{
    public async Task CreateListenerAsync(string name, uint port, byte[] x86Coff, byte[] x64Coff)
    {
        var client = new ListenerProtoService.ListenerProtoServiceClient(connection.GetChannel());
        await client.CreateListenerAsync(new CreateListenerRequest
        {
            Name = name,
            BindPort = port,
            X86Coff = ByteString.CopyFrom(x86Coff),
            X64Coff = ByteString.CopyFrom(x64Coff)
        }, connection.GetCallOptions());
    }

    public async Task DeleteListenerAsync(uint id)
    {
        var client = new ListenerProtoService.ListenerProtoServiceClient(connection.GetChannel());
        await client.DeleteListenerAsync(new DeleteListenerRequest { Id = id }, connection.GetCallOptions());
    }

    public AsyncServerStreamingCall<ListenerEvent> StreamEvents(CancellationToken ct)
    {
        var client = new ListenerProtoService.ListenerProtoServiceClient(connection.GetChannel());
        return client.ListenerEvents(new Empty(), connection.GetCallOptions(ct));
    }
}