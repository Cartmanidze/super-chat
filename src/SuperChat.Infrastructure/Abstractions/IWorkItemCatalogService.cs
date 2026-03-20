using SuperChat.Contracts.Features.WorkItems;

namespace SuperChat.Infrastructure.Abstractions;

public interface IWorkItemCatalogService
{
    Task<IReadOnlyList<WorkItemCardViewModel>> ListAsync(
        Guid userId,
        WorkItemType? type,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkItemCardViewModel>> SearchAsync(
        Guid userId,
        string query,
        WorkItemType? type,
        CancellationToken cancellationToken);
}
