namespace SuperChat.Contracts.Features.WorkItems;

public interface IMeetingWorkItemCommandService
{
    Task<bool> CompleteAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken);

    Task<bool> DismissAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken);
}
