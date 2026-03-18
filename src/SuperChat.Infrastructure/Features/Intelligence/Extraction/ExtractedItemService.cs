using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

internal sealed class ExtractedItemService(
    ExtractedItemIngestionService ingestionService,
    ExtractedItemQueryService queryService,
    ExtractedItemManualResolutionService manualResolutionService) : IExtractedItemService
{
    public Task AddRangeAsync(IEnumerable<ExtractedItem> items, CancellationToken cancellationToken)
    {
        return ingestionService.AddRangeAsync(items, cancellationToken);
    }

    public Task<IReadOnlyList<ExtractedItem>> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return queryService.GetForUserAsync(userId, cancellationToken);
    }

    public Task<IReadOnlyList<ExtractedItem>> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return queryService.GetActiveForUserAsync(userId, cancellationToken);
    }

    public Task<bool> CompleteAsync(Guid userId, Guid itemId, CancellationToken cancellationToken)
    {
        return manualResolutionService.CompleteAsync(userId, itemId, cancellationToken);
    }

    public Task<bool> DismissAsync(Guid userId, Guid itemId, CancellationToken cancellationToken)
    {
        return manualResolutionService.DismissAsync(userId, itemId, cancellationToken);
    }
}
