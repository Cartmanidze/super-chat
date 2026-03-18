using Microsoft.EntityFrameworkCore;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

internal sealed class WorkItemManualResolutionService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory)
{
    public Task<bool> CompleteAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, workItemId, WorkItemResolutionState.Completed, cancellationToken);
    }

    public Task<bool> DismissAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, workItemId, WorkItemResolutionState.Dismissed, cancellationToken);
    }

    private async Task<bool> ResolveAsync(
        Guid userId,
        Guid workItemId,
        string resolutionKind,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var target = await dbContext.WorkItems
            .SingleOrDefaultAsync(item => item.UserId == userId && item.Id == workItemId, cancellationToken);

        if (target is null)
        {
            return false;
        }

        var changed = false;
        var now = DateTimeOffset.UtcNow;

        var relatedItems = await dbContext.WorkItems
            .Where(item => item.UserId == userId && item.SourceEventId == target.SourceEventId)
            .ToListAsync(cancellationToken);

        foreach (var item in relatedItems)
        {
            changed |= ApplyResolution(item, resolutionKind, now);
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    private static bool ApplyResolution(
        WorkItemEntity entity,
        string resolutionKind,
        DateTimeOffset resolvedAt)
    {
        if (entity.IsResolved() &&
            string.Equals(entity.ResolutionKind, resolutionKind, StringComparison.Ordinal) &&
            string.Equals(entity.ResolutionSource, WorkItemResolutionState.Manual, StringComparison.Ordinal))
        {
            return false;
        }

        entity.ResolvedAt ??= resolvedAt;
        entity.ResolutionKind = resolutionKind;
        entity.ResolutionSource = WorkItemResolutionState.Manual;
        entity.UpdatedAt = resolvedAt;
        return true;
    }
}
