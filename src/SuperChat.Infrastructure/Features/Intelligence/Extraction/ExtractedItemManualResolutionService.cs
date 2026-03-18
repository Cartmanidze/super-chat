using Microsoft.EntityFrameworkCore;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

internal sealed class ExtractedItemManualResolutionService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory)
{
    public Task<bool> CompleteAsync(Guid userId, Guid itemId, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, itemId, WorkItemResolutionState.Completed, cancellationToken);
    }

    public Task<bool> DismissAsync(Guid userId, Guid itemId, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, itemId, WorkItemResolutionState.Dismissed, cancellationToken);
    }

    private async Task<bool> ResolveAsync(
        Guid userId,
        Guid itemId,
        string resolutionKind,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var target = await dbContext.ExtractedItems
            .SingleOrDefaultAsync(item => item.UserId == userId && item.Id == itemId, cancellationToken);

        if (target is null)
        {
            return false;
        }

        var changed = false;
        var now = DateTimeOffset.UtcNow;

        var relatedItems = await dbContext.ExtractedItems
            .Where(item => item.UserId == userId && item.SourceEventId == target.SourceEventId)
            .ToListAsync(cancellationToken);

        foreach (var item in relatedItems)
        {
            changed |= ApplyResolution(item, resolutionKind, now);
        }

        if (target.Kind == ExtractedItemKind.Meeting)
        {
            var relatedMeetings = await dbContext.Meetings
                .Where(item => item.UserId == userId && item.SourceEventId == target.SourceEventId)
                .ToListAsync(cancellationToken);

            foreach (var meeting in relatedMeetings)
            {
                changed |= ApplyResolution(meeting, resolutionKind, now);
            }
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    private static bool ApplyResolution(
        ExtractedItemEntity entity,
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
        return true;
    }

    private static bool ApplyResolution(
        MeetingEntity entity,
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
