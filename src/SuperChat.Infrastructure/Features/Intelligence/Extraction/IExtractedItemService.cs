using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Abstractions;

public interface IExtractedItemService
{
    Task AddRangeAsync(IEnumerable<ExtractedItem> items, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExtractedItem>> GetForUserAsync(Guid userId, CancellationToken cancellationToken);
}
