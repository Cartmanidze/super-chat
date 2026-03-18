using SuperChat.Contracts.ViewModels;

namespace SuperChat.Infrastructure.Abstractions;

public interface IWorkItemActionService
{
    Task<bool> CompleteAsync(Guid userId, WorkItemType type, string actionKey, CancellationToken cancellationToken);

    Task<bool> DismissAsync(Guid userId, WorkItemType type, string actionKey, CancellationToken cancellationToken);
}
