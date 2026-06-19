using CrystalC2.Tasks;
using Server.Core;
using Server.Utilities;
using TaskStatus = CrystalC2.Tasks.TaskStatus;

namespace Server.Tasks;

public sealed class BeaconTask : Entity
{
    public uint BeaconId { get; init; }
    public TaskType TaskType { get; init; }
    public byte[]? TaskData { get; init; }
    public string? CommandLine { get; init; }
    public string? User { get; init; }
    public TaskStatus Status { get; set; }
    public DateTimeOffset? StartTime { get; set; }
    
    public DateTimeOffset? EndTime { get; set; }

    public static BeaconTask Create(TaskRequest request, string? user)
    {
        return new BeaconTask
        {
            Id = Helpers.GenerateId(),
            BeaconId = request.BeaconId,
            TaskType = request.TaskType,
            TaskData = request.TaskData.ToByteArray(),
            CommandLine = request.CommandLine,
            User = user,
            Status = TaskStatus.Pending
        };
    }

    public void SetTasked()
    {
        Status = TaskStatus.Tasked;
        StartTime = DateTimeOffset.UtcNow;
    }

    public void SetComplete()
    {
        Status = TaskStatus.Complete;
        EndTime = DateTimeOffset.UtcNow;
    }
}