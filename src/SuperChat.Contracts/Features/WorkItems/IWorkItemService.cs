using SuperChat.Domain.Features.Intelligence;

namespace SuperChat.Contracts.Features.WorkItems;

public interface IWorkItemService
{
    Task AcceptRangeAsync(IEnumerable<ExtractedItem> items, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkItemRecord>> GetForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkItemRecord>> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkItemRecord>> SearchAsync(Guid userId, string query, int limit, CancellationToken cancellationToken);

    Task<bool> CompleteAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken);

    Task<bool> DismissAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken);
}
