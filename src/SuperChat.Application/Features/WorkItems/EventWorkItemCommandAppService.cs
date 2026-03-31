using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;

namespace SuperChat.Application.Features.WorkItems;

public sealed class EventWorkItemCommandAppService(
    IMeetingRepository meetingRepository,
    TimeProvider timeProvider) : IEventWorkItemCommandService
{
    public Task<bool> CompleteAsync(Guid userId, Guid eventId, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, eventId, "completed", cancellationToken);
    }

    public Task<bool> DismissAsync(Guid userId, Guid eventId, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, eventId, "dismissed", cancellationToken);
    }

    private async Task<bool> ResolveAsync(
        Guid userId,
        Guid eventId,
        string resolutionKind,
        CancellationToken cancellationToken)
    {
        var meeting = await meetingRepository.FindByIdAsync(userId, eventId, cancellationToken);
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
