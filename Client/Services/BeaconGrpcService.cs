using System.Threading;
using System.Threading.Tasks;
using CrystalC2.Beacons;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Client.Services;

internal sealed class BeaconGrpcService(ServerConnection connection)
{
    public AsyncServerStreamingCall<BeaconSession> StreamSessions(CancellationToken ct)
    {
        var client = new BeaconProtoService.BeaconProtoServiceClient(connection.GetChannel());
        return client.StreamSessions(new Empty(), connection.GetCallOptions(ct));
    }

    public async Task DeleteSessionAsync(uint beaconId)
    {
        var client = new BeaconProtoService.BeaconProtoServiceClient(connection.GetChannel());
        await client.DeleteSessionAsync(new DeleteSessionRequest { BeaconId = beaconId }, connection.GetCallOptions());
    }
}