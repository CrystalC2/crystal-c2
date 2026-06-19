using System.Buffers.Binary;
using CrystalC2.Beacons;
using CrystalC2.Tasks;
using Server.Beacons;
using Server.Core;
using Server.Tasks;
using TaskStatus = CrystalC2.Tasks.TaskStatus;

namespace Server.Listeners;

public sealed class C2Bridge(
    IRepository<Listener> listeners,
    IRepository<Beacon> beacons,
    IBroker<Beacon> beaconBroker,
    IRepository<BeaconTask> tasks,
    TaskBroker taskBroker)
{
    public async Task<byte[]> ProcessBeaconMessage(uint listenerId, byte[] data, CancellationToken ct)
    {
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);

        do
        {
            // read the callback type
            var bType = br.ReadBytes(sizeof(uint));
            var callbackType = BinaryPrimitives.ReadInt32BigEndian(bType.AsSpan());

            // read task / connection id
            var bId = br.ReadBytes(sizeof(uint));
            var taskId = BinaryPrimitives.ReadUInt32BigEndian(bId.AsSpan());

            // read data length
            var bLength = br.ReadBytes(sizeof(int));
            var length = BinaryPrimitives.ReadInt32BigEndian(bLength.AsSpan());

            // read the message
            var message = br.ReadBytes(length);

            switch (callbackType)
            {
                case 0x1: // beacon checkin
                {
                    var beacon = await HandleBeaconCheckin(listenerId, message, ct);

                    if (beacon is null)
                    {
                        break;
                    }

                    return await GetOutboundData(beacon, ct);
                }

                default: // task output
                {
                    var bComplete = br.ReadBytes(sizeof(int));
                    var complete = BinaryPrimitives.ReadInt32BigEndian(bComplete.AsSpan());

                    await HandleBeaconOutput(callbackType, taskId, message, complete, ct);

                    break;
                }
            }

        } while (ms.Position < ms.Length);

        return [];
    }

    private async Task<Beacon?> HandleBeaconCheckin(uint listenerId, byte[] data, CancellationToken ct)
    {
        var listener = await listeners.GetByIdAsync(listenerId, ct);

        if (listener is null)
        {
            return null;
        }

        var decrypted = Crypto.RsaDecrypt(data, listener.PrivateKey);
        var metadata = BeaconMetadata.Parse(decrypted);

        var beacon = await beacons.GetByIdAsync(metadata.Id, ct);

        if (beacon is null)
        {
            beacon = Beacon.Create(metadata, listener.Name);
            await beacons.AddAsync(beacon, ct);
        }
        else
        {
            beacon.CheckIn();
        }

        await beacons.UpdateAsync(beacon, ct);

        beaconBroker.Publish(beacon);

        return beacon;
    }

    private async Task<byte[]> GetOutboundData(Beacon parent, CancellationToken ct)
    {
        var spec = new ListPendingTasksSpec(parent.Id);
        var pending = await tasks.ListAsync(spec, ct);

        using var ms = new MemoryStream();

        foreach (var task in pending)
        {
            using var taskData = new MemoryStream();

            var taskId = new byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32BigEndian(taskId, task.Id);
            await taskData.WriteAsync(taskId.AsMemory(), ct);

            var taskType = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(taskType, (int)task.TaskType);
            await taskData.WriteAsync(taskType.AsMemory(), ct);

            var taskLen = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(taskLen, task.TaskData?.Length ?? 0);
            await taskData.WriteAsync(taskLen.AsMemory(), ct);

            await taskData.WriteAsync(task.TaskData?.AsMemory() ?? Array.Empty<byte>().AsMemory(), ct);

            var encrypted = Crypto.AesEncrypt(taskData.ToArray(), parent.SessionKey);

            var encryptedLen = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(encryptedLen, encrypted.Length);

            await ms.WriteAsync(encryptedLen.AsMemory(), ct);
            await ms.WriteAsync(encrypted.AsMemory(), ct);

            task.SetTasked();

            taskBroker.Publish(new BeaconCallback
            {
                TaskId = task.Id,
                Status = TaskStatus.Tasked,
                Output = []
            });
        }

        await tasks.UpdateRangeAsync(pending, ct);
        await tasks.SaveChangesAsync(ct);

        return ms.ToArray();
    }

    private async Task HandleBeaconOutput(int callbackType, uint taskId, byte[] taskData, int complete, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(taskId, ct);

        if (task is null)
            return;

        var beacon = await beacons.GetByIdAsync(task.BeaconId, ct);

        if (beacon is null)
            return;

        var plaintext = Crypto.AesDecrypt(taskData, beacon.SessionKey);

        var callback = new BeaconCallback
        {
            Type = callbackType,
            TaskId = taskId,
            Output = plaintext,
            Status = TaskStatus.Tasked
        };

        if (complete == 1)
        {
            callback.Status = callbackType == 0x0d ? TaskStatus.Error : TaskStatus.Complete;
            task.SetComplete();

            await tasks.UpdateAsync(task, ct);

            if (task.TaskType is TaskType.Exit)
            {
                beacon.SetHealth(BeaconHealth.Dead);

                await beacons.UpdateAsync(beacon, ct);
                beaconBroker.Publish(beacon);
            }

            await tasks.SaveChangesAsync(ct);
        }

        taskBroker.Publish(callback);
    }
}