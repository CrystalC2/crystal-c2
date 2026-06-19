using Ardalis.Specification;
using TaskStatus = CrystalC2.Tasks.TaskStatus;

namespace Server.Tasks;

public sealed class ListPendingTasksSpec : SingleResultSpecification<BeaconTask>
{
    public ListPendingTasksSpec(uint bid)
    {
        Query.Where(t => t.Status == TaskStatus.Pending && t.BeaconId == bid);
    }
}