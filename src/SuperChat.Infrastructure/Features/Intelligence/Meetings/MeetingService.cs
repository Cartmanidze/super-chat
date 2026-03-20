using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Features.Intelligence.Meetings;

internal sealed class MeetingService(
    MeetingUpsertService upsertService,
    MeetingUpcomingQueryService upcomingQueryService,
    MeetingManualResolutionService manualResolutionService) : IMeetingService
{
    public Task UpsertRangeAsync(IEnumerable<ExtractedItem> items, CancellationToken cancellationToken)
    {
        return upsertService.UpsertRangeAsync(items, cancellationToken);
    }

    public Task<IReadOnlyList<MeetingRecord>> GetUpcomingAsync(
        Guid userId,
        DateTimeOffset fromInclusive,
        int take,
        CancellationToken cancellationToken)
    {
        return upcomingQueryService.GetUpcomingAsync(userId, fromInclusive, take, cancellationToken);
    }

    public Task<bool> CompleteAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken)
    {
        return manualResolutionService.CompleteAsync(userId, meetingId, cancellationToken);
    }

    public Task<bool> DismissAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken)
    {
        return manualResolutionService.DismissAsync(userId, meetingId, cancellationToken);
    }
}
