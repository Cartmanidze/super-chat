using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Features.Intelligence.WorkItems;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Infrastructure.Features.Intelligence.Digest;

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

    private static bool IsRequest(WorkItemRecord item)
    {
        return WorkItemPresentationMetadata.ResolveType(item.Kind.ToString()) == WorkItemType.Request;
    }
}
