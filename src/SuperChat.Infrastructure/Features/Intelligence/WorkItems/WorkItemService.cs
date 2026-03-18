using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

internal sealed class WorkItemService(
    WorkItemIngestionService ingestionService,
    WorkItemQueryService queryService,
    WorkItemManualResolutionService manualResolutionService) : IWorkItemService
{
    public Task IngestRangeAsync(IEnumerable<ExtractedItem> items, CancellationToken cancellationToken)
    {
        return ingestionService.IngestRangeAsync(items, cancellationToken);
    }

    public Task<IReadOnlyList<WorkItemRecord>> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return queryService.GetForUserAsync(userId, cancellationToken);
    }

    public Task<IReadOnlyList<WorkItemRecord>> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return queryService.GetActiveForUserAsync(userId, cancellationToken);
    }

    public Task<bool> CompleteAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken)
    {
        return manualResolutionService.CompleteAsync(userId, workItemId, cancellationToken);
    }

    public Task<bool> DismissAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken)
    {
        return manualResolutionService.DismissAsync(userId, workItemId, cancellationToken);
    }
}
