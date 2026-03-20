using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Infrastructure.Features.Intelligence.Digest;

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

    private static bool IsRequest(WorkItemRecord item)
    {
        return WorkItemPresentationMetadata.ResolveType(item.Kind.ToString()) == WorkItemType.Request;
    }
}
