using SuperChat.Contracts.ViewModels;

namespace SuperChat.Infrastructure.Services;

internal sealed class RequestWorkItemTypeStrategy : IWorkItemTypeStrategy
{
    public WorkItemType Type => WorkItemType.Request;

    public IReadOnlyList<WorkItemCardViewModel> BuildCards(WorkItemStrategySnapshot snapshot)
    {
        return snapshot.WorkItems
            .Where(IsRequest)
            .OrderByDescending(item => item.ObservedAt)
            .Select(item => item.ToWorkItemCardViewModel(snapshot.Now).WithResolvedSourceRoom(snapshot.RoomNames))
            .ToList();
    }

    private static bool IsRequest(SuperChat.Domain.Model.WorkItemRecord item)
    {
        return WorkItemPresentationMetadata.ResolveType(item.Kind.ToString()) == WorkItemType.Request;
    }
}
