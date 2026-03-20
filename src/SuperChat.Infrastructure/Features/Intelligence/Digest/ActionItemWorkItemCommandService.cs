using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Features.Intelligence.WorkItems;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Infrastructure.Features.Intelligence.Digest;

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

    private static bool IsActionItem(WorkItemRecord item)
    {
        return WorkItemPresentationMetadata.ResolveType(item.Kind.ToString()) == WorkItemType.ActionItem;
    }
}
