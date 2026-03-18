using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Abstractions;

public interface IWorkItemService
{
    Task IngestRangeAsync(IEnumerable<ExtractedItem> items, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkItemRecord>> GetForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkItemRecord>> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> CompleteAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken);

    Task<bool> DismissAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken);
}
