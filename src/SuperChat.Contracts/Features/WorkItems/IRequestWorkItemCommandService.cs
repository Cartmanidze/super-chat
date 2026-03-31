namespace SuperChat.Contracts.Features.WorkItems;

public interface IRequestWorkItemCommandService
{
    Task<bool> CompleteAsync(Guid userId, Guid requestId, CancellationToken cancellationToken);

    Task<bool> DismissAsync(Guid userId, Guid requestId, CancellationToken cancellationToken);
}
