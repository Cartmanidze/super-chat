using SuperChat.Contracts.ViewModels;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

internal sealed class RequestWorkItemTypeStrategy(
    IExtractedItemService extractedItemService,
    ExtractedItemLookupService extractedItemLookupService) : IWorkItemTypeStrategy
{
    public WorkItemType Type => WorkItemType.Request;

    public IReadOnlyList<WorkItemCardViewModel> BuildCards(WorkItemStrategySnapshot snapshot)
    {
        return snapshot.ExtractedItems
            .Where(IsRequest)
            .OrderByDescending(item => item.ObservedAt)
            .Select(item => item.ToWorkItemCardViewModel(snapshot.Now).WithResolvedSourceRoom(snapshot.RoomNames))
            .ToList();
    }

    public Task<bool> CompleteAsync(Guid userId, string actionKey, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, actionKey, extractedItemService.CompleteAsync, cancellationToken);
    }

    public Task<bool> DismissAsync(Guid userId, string actionKey, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, actionKey, extractedItemService.DismissAsync, cancellationToken);
    }

    private async Task<bool> ResolveAsync(
        Guid userId,
        string actionKey,
        Func<Guid, Guid, CancellationToken, Task<bool>> resolveAsync,
        CancellationToken cancellationToken)
    {
        if (!WorkItemActionKey.TryParse(actionKey, out var target, out var id) ||
            target is not WorkItemActionTarget.ExtractedItem)
        {
            return false;
        }

        var item = await extractedItemLookupService.GetByIdAsync(userId, id, cancellationToken);
        return item is not null && IsRequest(item) &&
               await resolveAsync(userId, id, cancellationToken);
    }

    private static bool IsRequest(SuperChat.Domain.Model.ExtractedItem item)
    {
        return WorkItemPresentationMetadata.ResolveType(item.Kind.ToString()) == WorkItemType.Request;
    }
}
