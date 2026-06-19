namespace Server.Tasks;
using TaskStatus = CrystalC2.Tasks.TaskStatus;

/// <summary>
/// BeaconOutput from a Beacon
/// </summary>
public sealed class BeaconCallback
{
    public int Type { get; set; }
    public uint TaskId { get; set; }
    public byte[]? Output { get; set; }
    public TaskStatus Status { get; set; }
}