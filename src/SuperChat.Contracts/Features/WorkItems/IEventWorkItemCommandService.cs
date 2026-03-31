namespace SuperChat.Contracts.Features.WorkItems;

public interface IEventWorkItemCommandService
{
    Task<bool> CompleteAsync(Guid userId, Guid eventId, CancellationToken cancellationToken);

    Task<bool> DismissAsync(Guid userId, Guid eventId, CancellationToken cancellationToken);
}
