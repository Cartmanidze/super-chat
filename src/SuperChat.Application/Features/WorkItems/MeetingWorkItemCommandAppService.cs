using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;

namespace SuperChat.Application.Features.WorkItems;

public sealed class MeetingWorkItemCommandAppService(
    IMeetingRepository meetingRepository,
    TimeProvider timeProvider) : IMeetingWorkItemCommandService
{
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

        await meetingRepository.ResolveAsync(
            meeting.Id,
            resolutionKind,
            "manual",
            timeProvider.GetUtcNow(),
            cancellationToken);

        return true;
    }
}
