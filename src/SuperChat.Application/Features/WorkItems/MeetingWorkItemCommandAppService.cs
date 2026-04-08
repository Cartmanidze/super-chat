using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;
using DomainMeetingStatus = SuperChat.Domain.Features.Intelligence.MeetingStatus;

namespace SuperChat.Application.Features.WorkItems;

public sealed class MeetingWorkItemCommandAppService(
    IMeetingRepository meetingRepository,
    TimeProvider timeProvider) : IMeetingWorkItemCommandService
{
    public Task<bool> ConfirmAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken)
    {
        return UpdateStatusAsync(userId, meetingId, DomainMeetingStatus.Confirmed, cancellationToken);
    }

    public Task<bool> UnconfirmAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken)
    {
        return UpdateStatusAsync(userId, meetingId, DomainMeetingStatus.PendingConfirmation, cancellationToken);
    }

    public Task<bool> CompleteAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, meetingId, "completed", cancellationToken);
    }

    public Task<bool> DismissAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, meetingId, "dismissed", cancellationToken);
    }

    private async Task<bool> ResolveAsync(
        Guid userId,
        Guid meetingId,
        string resolutionKind,
        CancellationToken cancellationToken)
    {
        var meeting = await meetingRepository.FindByIdAsync(userId, meetingId, cancellationToken);
        if (meeting is null)
        {
            return false;
        }

        await meetingRepository.ResolveRelatedAsync(
            userId,
            meeting.SourceEventId,
            resolutionKind,
            "manual",
            timeProvider.GetUtcNow(),
            cancellationToken);

        return true;
    }

    private async Task<bool> UpdateStatusAsync(
        Guid userId,
        Guid meetingId,
        DomainMeetingStatus status,
        CancellationToken cancellationToken)
    {
        var meeting = await meetingRepository.FindByIdAsync(userId, meetingId, cancellationToken);
        if (meeting is null)
        {
            return false;
        }

        await meetingRepository.UpdateStatusAsync(
            userId,
            meetingId,
            status,
            timeProvider.GetUtcNow(),
            cancellationToken);

        return true;
    }
}
