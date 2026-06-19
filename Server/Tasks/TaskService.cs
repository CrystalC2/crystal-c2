using System.Threading.Channels;
using CrystalC2.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Server.Core;
using TaskStatus = CrystalC2.Tasks.TaskStatus;

namespace Server.Tasks;

[Authorize]
public sealed class TaskService(
    IRepository<BeaconTask> taskDb,
    TaskBroker taskBroker)
    : TaskProtoService.TaskProtoServiceBase
{
    public override async Task StreamTasks(IAsyncStreamReader<TaskRequest> requestStream,
        IServerStreamWriter<TaskResponse> responseStream, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var user = context.GetHttpContext().User;

        // One channel per connection — all task outputs for this client flow through here.
        var clientChannel = Channel.CreateUnbounded<BeaconCallback>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        try
        {
            // Read incoming TaskRequests concurrently with the response write loop below.
            var readLoop = ReadRequestsAsync(requestStream, responseStream, clientChannel, user.Identity?.Name, ct);

            // Forward task output to the client as TaskResponse messages.
            await foreach (var output in clientChannel.Reader.ReadAllAsync(ct))
            {
                await responseStream.WriteAsync(ToResponse(output), ct);
            }

            await readLoop;
        }
        catch (Exception e) when (e is OperationCanceledException or IOException)
        {
            //
        }
    }

    private async Task ReadRequestsAsync(IAsyncStreamReader<TaskRequest> requestStream, IServerStreamWriter<TaskResponse> responseStream, Channel<BeaconCallback> clientChannel, string? name, CancellationToken ct)
    {
        var registrations = new List<IDisposable>();

        try
        {
            while (await requestStream.MoveNext(ct))
            {
                var beaconTask = BeaconTask.Create(requestStream.Current, name);

                await taskDb.AddAsync(beaconTask, ct);
                await taskDb.SaveChangesAsync(ct);

                registrations.Add(taskBroker.Register(beaconTask.Id, clientChannel.Writer));

                // send a 'pending' back straight away
                await responseStream.WriteAsync(new TaskResponse
                {
                    TaskId = beaconTask.Id,
                    Status = TaskStatus.Pending,
                    Output = ByteString.Empty,
                    Timestamp = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
                }, ct);
            }
        }
        catch (IOException)
        {
            //
        }
        finally
        {
            // Unregister all task IDs for this client and signal the response loop to finish.
            foreach (var reg in registrations)
            {
                reg.Dispose();
            }

            clientChannel.Writer.TryComplete();
        }
    }

    private static TaskResponse ToResponse(BeaconCallback output) => new()
    {
        TaskId = output.TaskId,
        Status = output.Status,
        Output = output.Output is not null ? ByteString.CopyFrom(output.Output) : null,
        Timestamp = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
    };
}