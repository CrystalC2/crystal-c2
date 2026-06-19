using System.Threading;
using System.Threading.Tasks;
using CrystalC2.Payloads;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Client.Services;

internal sealed class PayloadGrpcService(ServerConnection connection)
{
    public async Task AddPayloadAsync(string name, byte[] bytes, string? yara, string? note)
    {
        var client = new PayloadProtoService.PayloadProtoServiceClient(connection.GetChannel());
        var request = new AddPayloadRequest
        {
            Name    = name,
            Payload = ByteString.CopyFrom(bytes)
        };

        if (!string.IsNullOrWhiteSpace(yara))
        {
            request.Yara = yara;
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            request.Note = note;
        }

        await client.AddPayloadAsync(request, connection.GetCallOptions());
    }

    public async Task DeletePayloadAsync(uint id)
    {
        var client = new PayloadProtoService.PayloadProtoServiceClient(connection.GetChannel());
        await client.DeletePayloadAsync(new DeletePayloadRequest { Id = id }, connection.GetCallOptions());
    }

    public AsyncServerStreamingCall<PayloadEvent> StreamEvents(CancellationToken ct)
    {
        var client = new PayloadProtoService.PayloadProtoServiceClient(connection.GetChannel());
        return client.PayloadEvents(new Empty(), connection.GetCallOptions(ct));
    }
}