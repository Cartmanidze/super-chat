using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Abstractions;

public interface IMeetingService
{
    Task UpsertRangeAsync(IEnumerable<ExtractedItem> items, CancellationToken cancellationToken);

    Task<IReadOnlyList<MeetingRecord>> GetUpcomingAsync(Guid userId, DateTimeOffset fromInclusive, int take, CancellationToken cancellationToken);

    Task<bool> CompleteAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken);

    Task<bool> DismissAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken);
}
