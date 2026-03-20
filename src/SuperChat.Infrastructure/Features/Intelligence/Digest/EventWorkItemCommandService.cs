using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Features.Intelligence.Meetings;

namespace SuperChat.Infrastructure.Features.Intelligence.Digest;

internal sealed class EventWorkItemCommandService(
    IMeetingService meetingService,
    MeetingLookupService meetingLookupService) : IEventWorkItemCommandService
{
    public Task<bool> CompleteAsync(Guid userId, Guid eventId, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, eventId, isComplete: true, cancellationToken);
    }

    public Task<bool> DismissAsync(Guid userId, Guid eventId, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, eventId, isComplete: false, cancellationToken);
    }

    private async Task<bool> ResolveAsync(
        Guid userId,
        Guid eventId,
        bool isComplete,
        CancellationToken cancellationToken)
    {
        var meeting = await meetingLookupService.GetByIdAsync(userId, eventId, cancellationToken);
        if (meeting is null)
        {
            return false;
        }

        return isComplete
            ? await meetingService.CompleteAsync(userId, meeting.Id, cancellationToken)
            : await meetingService.DismissAsync(userId, meeting.Id, cancellationToken);
    }
}
