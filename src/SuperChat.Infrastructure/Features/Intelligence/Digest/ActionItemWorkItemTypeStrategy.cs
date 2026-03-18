using SuperChat.Contracts.ViewModels;

namespace SuperChat.Infrastructure.Services;

internal sealed class ActionItemWorkItemTypeStrategy : IWorkItemTypeStrategy
{
    public WorkItemType Type => WorkItemType.ActionItem;

    public IReadOnlyList<WorkItemCardViewModel> BuildCards(WorkItemStrategySnapshot snapshot)
    {
        return snapshot.WorkItems
            .Where(IsActionItem)
            .OrderBy(item => item.DueAt ?? DateTimeOffset.MaxValue)
            .ThenByDescending(item => item.Confidence)
            .ThenByDescending(item => item.ObservedAt)
            .Select(item => item.ToWorkItemCardViewModel(snapshot.Now).WithResolvedSourceRoom(snapshot.RoomNames))
            .ToList();
    }

    private static bool IsActionItem(SuperChat.Domain.Model.WorkItemRecord item)
    {
        return WorkItemPresentationMetadata.ResolveType(item.Kind.ToString()) == WorkItemType.ActionItem;
    }
}
