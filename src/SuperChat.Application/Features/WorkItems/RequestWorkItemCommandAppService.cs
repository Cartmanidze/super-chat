using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;

namespace SuperChat.Application.Features.WorkItems;

public sealed class RequestWorkItemCommandAppService(
    IWorkItemRepository workItemRepository,
    TimeProvider timeProvider) : IRequestWorkItemCommandService
{
    public Task<bool> CompleteAsync(Guid userId, Guid requestId, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, requestId, "completed", cancellationToken);
    }

    public Task<bool> DismissAsync(Guid userId, Guid requestId, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, requestId, "dismissed", cancellationToken);
    }

    private async Task<bool> ResolveAsync(
        Guid userId,
        Guid requestId,
        string resolutionKind,
        CancellationToken cancellationToken)
    {
        var item = await workItemRepository.FindByIdAsync(userId, requestId, cancellationToken);
        if (item is null || !IsRequest(item))
        {
            return false;
        }

        await workItemRepository.ResolveAsync(
            requestId,
            resolutionKind,
            "manual",
            timeProvider.GetUtcNow(),
            cancellationToken);

        return true;
    }

    private static bool IsRequest(WorkItemRecord item)
    {
        return item.Kind is ExtractedItemKind.WaitingOn;
    }
}
