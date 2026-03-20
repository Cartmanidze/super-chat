using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Features.Intelligence.Digest;

internal sealed class WorkItemCatalogService(
    IEnumerable<IWorkItemTypeStrategy> strategies,
    WorkItemStrategySnapshotProvider snapshotProvider) : IWorkItemCatalogService
{
    private readonly IReadOnlyList<IWorkItemTypeStrategy> _strategies = strategies
        .OrderBy(item => item.Type)
        .ToList();

    public async Task<IReadOnlyList<WorkItemCardViewModel>> ListAsync(
        Guid userId,
        WorkItemType? type,
        CancellationToken cancellationToken)
    {
        var snapshot = await snapshotProvider.CreateAsync(userId, cancellationToken);
        return SelectStrategies(type)
            .SelectMany(strategy => strategy.BuildCards(snapshot))
            .OrderByDescending(item => item.Priority == WorkItemPriority.Important)
            .ThenBy(item => item.PlannedAt ?? item.DueAt ?? DateTimeOffset.MaxValue)
            .ThenByDescending(item => item.ObservedAt)
            .ToList();
    }

    public async Task<IReadOnlyList<WorkItemCardViewModel>> SearchAsync(
        Guid userId,
        string query,
        WorkItemType? type,
        CancellationToken cancellationToken)
    {
        var normalizedQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return [];
        }

        var cards = await ListAsync(userId, type, cancellationToken);
        return cards
            .Where(item => Matches(item, normalizedQuery))
            .ToList();
    }

    private IEnumerable<IWorkItemTypeStrategy> SelectStrategies(WorkItemType? type)
    {
        return type is null
            ? _strategies
            : _strategies.Where(item => item.Type == type.Value);
    }

    private static bool Matches(WorkItemCardViewModel card, string query)
    {
        return card.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               card.Summary.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               card.SourceRoom.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               card.Kind.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               card.Type?.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) == true ||
               card.MeetingProvider?.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) == true;
    }
}
