using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;

namespace SuperChat.Application.Features.WorkItems;

public sealed class ActionItemWorkItemCommandAppService(
    IWorkItemRepository workItemRepository,
    TimeProvider timeProvider) : IActionItemWorkItemCommandService
{
    public Task<bool> CompleteAsync(Guid userId, Guid actionItemId, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, actionItemId, "completed", cancellationToken);
    }

    public Task<bool> DismissAsync(Guid userId, Guid actionItemId, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, actionItemId, "dismissed", cancellationToken);
    }

    private async Task<bool> ResolveAsync(
        Guid userId,
        Guid actionItemId,
        string resolutionKind,
        CancellationToken cancellationToken)
    {
        var item = await workItemRepository.FindByIdAsync(userId, actionItemId, cancellationToken);
        if (item is null || !IsActionItem(item))
        {
            return false;
        }

        await workItemRepository.ResolveAsync(
            actionItemId,
            resolutionKind,
            "manual",
            timeProvider.GetUtcNow(),
            cancellationToken);

        return true;
    }

    private static bool IsActionItem(WorkItemRecord item)
    {
        return item.Kind is ExtractedItemKind.Task or ExtractedItemKind.Commitment;
    }
}
