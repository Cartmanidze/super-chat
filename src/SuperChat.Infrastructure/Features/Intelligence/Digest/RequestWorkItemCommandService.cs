using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

internal sealed class RequestWorkItemCommandService(
    IWorkItemService workItemService,
    WorkItemLookupService workItemLookupService) : IRequestWorkItemCommandService
{
    public Task<bool> CompleteAsync(Guid userId, Guid requestId, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, requestId, workItemService.CompleteAsync, cancellationToken);
    }

    public Task<bool> DismissAsync(Guid userId, Guid requestId, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, requestId, workItemService.DismissAsync, cancellationToken);
    }

    private async Task<bool> ResolveAsync(
        Guid userId,
        Guid requestId,
        Func<Guid, Guid, CancellationToken, Task<bool>> resolveAsync,
        CancellationToken cancellationToken)
    {
        var item = await workItemLookupService.GetByIdAsync(userId, requestId, cancellationToken);
        return item is not null && IsRequest(item) &&
               await resolveAsync(userId, requestId, cancellationToken);
    }

    private static bool IsRequest(SuperChat.Domain.Model.WorkItemRecord item)
    {
        return WorkItemPresentationMetadata.ResolveType(item.Kind.ToString()) == SuperChat.Contracts.ViewModels.WorkItemType.Request;
    }
}
