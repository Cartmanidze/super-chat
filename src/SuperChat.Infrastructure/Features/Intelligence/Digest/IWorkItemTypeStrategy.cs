using SuperChat.Contracts.Features.WorkItems;

namespace SuperChat.Infrastructure.Features.Intelligence.Digest;

internal interface IWorkItemTypeStrategy
{
    WorkItemType Type { get; }

    IReadOnlyList<WorkItemCardViewModel> BuildCards(WorkItemStrategySnapshot snapshot);
}
