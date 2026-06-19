using System.Text;
using CrystalC2.Payloads;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Server.Core;

namespace Server.Payloads;

internal sealed class PayloadService(
    ILogger<PayloadService> logger,
    IRepository<Payload> db,
    IBroker<PayloadEvent> broker)
    : PayloadProtoService.PayloadProtoServiceBase
{
    public override async Task<Empty> AddPayload(AddPayloadRequest request, ServerCallContext ctx)
    {
        var username = ctx.GetHttpContext().User.Identity?.Name ?? string.Empty;

        var payload = Payload.Create(
            request.Name,
            username,
            request.Payload.ToByteArray(),
            request.Yara,
            request.Note);
        
        await db.AddAsync(payload, ctx.CancellationToken);
        await db.SaveChangesAsync(ctx.CancellationToken);
        
        logger.LogInformation("Added payload '{PayloadName}'", payload.Name);
        
        broker.Publish(new PayloadEvent
        {
            Payload = ToPayloadInfo(payload),
            Type = PayloadEventType.PayloadEventAdded
        });
        
        return new Empty();
    }

    public override async Task PayloadEvents(Empty req, IServerStreamWriter<PayloadEvent> resp, ServerCallContext ctx)
    {
        // stream first bunch from the db
        var payloads = await db.ListAsync(ctx.CancellationToken);

        foreach (var payload in payloads)
        {
            await resp.WriteAsync(new PayloadEvent
            {
                Payload = ToPayloadInfo(payload),
                Type = PayloadEventType.PayloadEventAdded
            });
        }

        // set up a pub/sub to get notifications of new creations and deletions
        var (reader, subscription) = broker.Subscribe();

        using (subscription)
        {
            await foreach (var listenerEvent in reader.ReadAllAsync(ctx.CancellationToken))
            {
                await resp.WriteAsync(listenerEvent, ctx.CancellationToken);
            }
        }
    }

    public override async Task<Empty> DeletePayload(DeletePayloadRequest request, ServerCallContext context)
    {
        var payload = await db.GetByIdAsync(request.Id, context.CancellationToken);

        if (payload is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Payload not found"));
        }
        
        // delete from db
        await db.DeleteAsync(payload, context.CancellationToken);
        
        logger.LogInformation("Deleted payload '{PayloadName}'", payload.Name);
        
        // publish an event
        broker.Publish(new PayloadEvent
        {
            Payload = ToPayloadInfo(payload),
            Type = PayloadEventType.PayloadEventDeleted
        });
        
        return new Empty();
    }

    private static PayloadInfo ToPayloadInfo(Payload payload)
    {
        var info = new PayloadInfo
        {
            Id = payload.Id,
            Name = payload.Name,
            User = payload.User,
            Payload = ByteString.CopyFrom(payload.Bytes)
        };

        if (payload.Yara is not null)
        {
            info.Yara = Encoding.UTF8.GetString(payload.Yara);
        }

        if (!string.IsNullOrWhiteSpace(payload.Note))
        {
            info.Note = payload.Note;
        }
        
        return info;
    }
}