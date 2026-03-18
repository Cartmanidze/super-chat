using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

internal sealed class ActionItemWorkItemCommandService(
    IWorkItemService workItemService,
    WorkItemLookupService workItemLookupService) : IActionItemWorkItemCommandService
{
    public Task<bool> CompleteAsync(Guid userId, Guid actionItemId, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, actionItemId, workItemService.CompleteAsync, cancellationToken);
    }

    public Task<bool> DismissAsync(Guid userId, Guid actionItemId, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, actionItemId, workItemService.DismissAsync, cancellationToken);
    }

    private async Task<bool> ResolveAsync(
        Guid userId,
        Guid actionItemId,
        Func<Guid, Guid, CancellationToken, Task<bool>> resolveAsync,
        CancellationToken cancellationToken)
    {
        var item = await workItemLookupService.GetByIdAsync(userId, actionItemId, cancellationToken);
        return item is not null && IsActionItem(item) &&
               await resolveAsync(userId, actionItemId, cancellationToken);
    }

    private static bool IsActionItem(SuperChat.Domain.Model.WorkItemRecord item)
    {
        return WorkItemPresentationMetadata.ResolveType(item.Kind.ToString()) == SuperChat.Contracts.ViewModels.WorkItemType.ActionItem;
    }
}
