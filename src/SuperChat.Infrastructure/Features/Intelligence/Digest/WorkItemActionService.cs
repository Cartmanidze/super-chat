using SuperChat.Contracts.ViewModels;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

internal sealed class WorkItemActionService(
    IEnumerable<IWorkItemTypeStrategy> strategies) : IWorkItemActionService
{
    private readonly IReadOnlyDictionary<WorkItemType, IWorkItemTypeStrategy> _strategies = strategies
        .ToDictionary(item => item.Type);

    public Task<bool> CompleteAsync(Guid userId, WorkItemType type, string actionKey, CancellationToken cancellationToken)
    {
        return ResolveByTypeAsync(userId, type, actionKey, isComplete: true, cancellationToken);
    }

    public Task<bool> DismissAsync(Guid userId, WorkItemType type, string actionKey, CancellationToken cancellationToken)
    {
        return ResolveByTypeAsync(userId, type, actionKey, isComplete: false, cancellationToken);
    }

    private Task<bool> ResolveByTypeAsync(
        Guid userId,
        WorkItemType type,
        string actionKey,
        bool isComplete,
        CancellationToken cancellationToken)
    {
        return !_strategies.TryGetValue(type, out var strategy)
            ? Task.FromResult(false)
            : isComplete
                ? strategy.CompleteAsync(userId, actionKey, cancellationToken)
                : strategy.DismissAsync(userId, actionKey, cancellationToken);
    }
}
