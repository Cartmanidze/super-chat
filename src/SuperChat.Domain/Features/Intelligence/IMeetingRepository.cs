namespace SuperChat.Domain.Features.Intelligence;

public interface IMeetingRepository
{
    Task<MeetingRecord?> FindByIdAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken);
    Task<MeetingRecord?> FindBySourceEventIdAsync(Guid userId, string sourceEventId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MeetingRecord>> GetUnresolvedAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MeetingRecord>> GetUpcomingAsync(Guid userId, DateTimeOffset from, int take, CancellationToken cancellationToken);
    Task UpsertRangeAsync(IReadOnlyList<MeetingRecord> meetings, CancellationToken cancellationToken);
    Task ResolveAsync(Guid meetingId, string resolutionKind, string resolutionSource, DateTimeOffset resolvedAt, CancellationToken cancellationToken);
}
