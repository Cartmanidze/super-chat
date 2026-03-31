namespace SuperChat.Contracts.Features.WorkItems;

public interface IActionItemWorkItemCommandService
{
    Task<bool> CompleteAsync(Guid userId, Guid actionItemId, CancellationToken cancellationToken);

    Task<bool> DismissAsync(Guid userId, Guid actionItemId, CancellationToken cancellationToken);
}
