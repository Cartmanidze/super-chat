namespace SuperChat.Infrastructure.Abstractions;

public interface IEventWorkItemCommandService
{
    Task<bool> CompleteAsync(Guid userId, Guid eventId, CancellationToken cancellationToken);

    Task<bool> DismissAsync(Guid userId, Guid eventId, CancellationToken cancellationToken);
}
