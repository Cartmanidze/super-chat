using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Abstractions;

public interface IExtractedItemService
{
    Task AddRangeAsync(IEnumerable<ExtractedItem> items, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExtractedItem>> GetForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExtractedItem>> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> CompleteAsync(Guid userId, Guid itemId, CancellationToken cancellationToken);

    Task<bool> DismissAsync(Guid userId, Guid itemId, CancellationToken cancellationToken);
}
