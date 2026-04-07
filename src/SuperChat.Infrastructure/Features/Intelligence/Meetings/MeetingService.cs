using SuperChat.Contracts.Features.Intelligence.Meetings;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Infrastructure.Features.Intelligence.Meetings;

internal sealed class MeetingService(
    MeetingUpsertService upsertService,
    IMeetingRepository meetingRepository,
    TimeProvider timeProvider) : IMeetingService
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
        return meetingRepository.GetUpcomingAsync(userId, fromInclusive, take, cancellationToken);
    }

    public Task<bool> CompleteAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, meetingId, WorkItemResolutionState.Completed, cancellationToken);
    }

    public Task<bool> DismissAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, meetingId, WorkItemResolutionState.Dismissed, cancellationToken);
    }

    private async Task<bool> ResolveAsync(
        Guid userId,
        Guid meetingId,
        string resolutionKind,
        CancellationToken cancellationToken)
    {
        var target = await meetingRepository.FindByIdAsync(userId, meetingId, cancellationToken);
        if (target is null)
        {
            return false;
        }

        await meetingRepository.ResolveRelatedAsync(
            userId,
            target.SourceEventId,
            resolutionKind,
            WorkItemResolutionState.Manual,
            timeProvider.GetUtcNow(),
            cancellationToken);

        return true;
    }
}
