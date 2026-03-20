using Microsoft.EntityFrameworkCore;
using SuperChat.Infrastructure.Shared.Persistence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Infrastructure.Features.Intelligence.Meetings;

internal sealed class MeetingManualResolutionService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory)
{
    public Task<bool> CompleteAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, meetingId, WorkItemResolutionState.Completed, cancellationToken);
    }

    public Task<bool> DismissAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken)
    {
        return ResolveAsync(userId, meetingId, WorkItemResolutionState.Dismissed, cancellationToken);
    }

    private async Task<bool> ResolveAsync(
        Guid userId,
        Guid meetingId,
        string resolutionKind,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var target = await dbContext.Meetings
            .SingleOrDefaultAsync(item => item.UserId == userId && item.Id == meetingId, cancellationToken);

        if (target is null)
        {
            return false;
        }

        var changed = false;
        var now = DateTimeOffset.UtcNow;

        var relatedMeetings = await dbContext.Meetings
            .Where(item => item.UserId == userId && item.SourceEventId == target.SourceEventId)
            .ToListAsync(cancellationToken);

        foreach (var meeting in relatedMeetings)
        {
            changed |= ApplyResolution(meeting, resolutionKind, now);
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

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
