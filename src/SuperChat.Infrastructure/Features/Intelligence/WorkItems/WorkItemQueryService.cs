using Microsoft.EntityFrameworkCore;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

internal sealed class WorkItemQueryService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    WorkItemAutoResolutionService autoResolutionService)
{
    public Task<IReadOnlyList<WorkItemRecord>> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return GetItemsAsync(userId, unresolvedOnly: false, autoResolve: false, cancellationToken);
    }

    public Task<IReadOnlyList<WorkItemRecord>> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return GetItemsAsync(userId, unresolvedOnly: true, autoResolve: true, cancellationToken);
    }

    private async Task<IReadOnlyList<WorkItemRecord>> GetItemsAsync(
        Guid userId,
        bool unresolvedOnly,
        bool autoResolve,
        CancellationToken cancellationToken)
    {
        if (autoResolve)
        {
            await autoResolutionService.ResolveAsync(userId, cancellationToken);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await dbContext.WorkItems
            .AsNoTracking()
            .Where(item => item.UserId == userId && (!unresolvedOnly || item.ResolvedAt == null))
            .ToListAsync(cancellationToken);

        return entities
            .Where(ExtractedItemFilters.ShouldKeep)
            .OrderByDescending(item => item.ObservedAt)
            .Select(item => item.ToDomain())
            .ToList();
    }
}
