using SuperChat.Contracts.ViewModels;

namespace SuperChat.Infrastructure.Services;

internal sealed class EventWorkItemTypeStrategy : IWorkItemTypeStrategy
{
    public WorkItemType Type => WorkItemType.Event;

    public IReadOnlyList<WorkItemCardViewModel> BuildCards(WorkItemStrategySnapshot snapshot)
    {
        return snapshot.Meetings
            .OrderBy(item => item.ScheduledFor)
            .ThenByDescending(item => item.Confidence)
            .Select(item => item.ToWorkItemCardViewModel(snapshot.Now).WithResolvedSourceRoom(snapshot.RoomNames))
            .ToList();
    }
}
