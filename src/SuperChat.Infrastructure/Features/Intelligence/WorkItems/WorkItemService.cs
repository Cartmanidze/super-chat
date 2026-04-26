using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Infrastructure.Features.Intelligence.WorkItems;

internal sealed class WorkItemService(
    WorkItemWriter writer,
    IWorkItemRepository workItemRepository,
    TimeProvider timeProvider) : IWorkItemService
{
    public Task AcceptRangeAsync(IEnumerable<ExtractedItem> items, CancellationToken cancellationToken)
    {
        return writer.AcceptRangeAsync(items, cancellationToken);
    }

    public Task<IReadOnlyList<WorkItemRecord>> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return workItemRepository.GetByUserAsync(userId, unresolvedOnly: false, cancellationToken);
    }

    public Task<IReadOnlyList<WorkItemRecord>> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return workItemRepository.GetByUserAsync(userId, unresolvedOnly: true, cancellationToken);
    }

    public Task<IReadOnlyList<WorkItemRecord>> SearchAsync(Guid userId, string query, int limit, CancellationToken cancellationToken)
    {
        return workItemRepository.SearchAsync(userId, query, limit, cancellationToken);
    }

    public Task<bool> CompleteAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, workItemId, WorkItemResolutionState.Completed, cancellationToken);
    }

    public Task<bool> DismissAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, workItemId, WorkItemResolutionState.Dismissed, cancellationToken);
    }

    private async Task<bool> ResolveAsync(
        Guid userId,
        Guid workItemId,
        string resolutionKind,
        CancellationToken cancellationToken)
    {
        var target = await workItemRepository.FindByIdAsync(userId, workItemId, cancellationToken);
        if (target is null)
        {
            return false;
        }

        await workItemRepository.ResolveRelatedAsync(
            userId,
            target.SourceEventId,
            resolutionKind,
            WorkItemResolutionState.Manual,
            timeProvider.GetUtcNow(),
            cancellationToken);

        return true;
    }
}
