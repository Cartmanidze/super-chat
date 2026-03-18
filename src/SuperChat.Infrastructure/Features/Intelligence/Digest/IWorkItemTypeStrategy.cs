using SuperChat.Contracts.ViewModels;

namespace SuperChat.Infrastructure.Services;

internal interface IWorkItemTypeStrategy
{
    WorkItemType Type { get; }

    IReadOnlyList<WorkItemCardViewModel> BuildCards(WorkItemStrategySnapshot snapshot);
}
