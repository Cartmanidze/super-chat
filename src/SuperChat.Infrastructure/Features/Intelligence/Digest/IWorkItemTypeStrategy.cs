using SuperChat.Contracts.ViewModels;

namespace SuperChat.Infrastructure.Services;

internal interface IWorkItemTypeStrategy
{
    WorkItemType Type { get; }

    IReadOnlyList<WorkItemCardViewModel> BuildCards(WorkItemStrategySnapshot snapshot);

    Task<bool> CompleteAsync(Guid userId, string actionKey, CancellationToken cancellationToken);

    Task<bool> DismissAsync(Guid userId, string actionKey, CancellationToken cancellationToken);
}
