using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Abstractions;

public interface IMeetingService
{
    Task UpsertRangeAsync(IEnumerable<ExtractedItem> items, CancellationToken cancellationToken);

    Task<IReadOnlyList<MeetingRecord>> GetUpcomingAsync(Guid userId, DateTimeOffset fromInclusive, int take, CancellationToken cancellationToken);
}
