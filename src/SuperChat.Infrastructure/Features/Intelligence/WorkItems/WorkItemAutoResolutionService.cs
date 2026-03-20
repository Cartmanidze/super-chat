using Microsoft.EntityFrameworkCore;
using SuperChat.Infrastructure.Shared.Persistence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Infrastructure.Features.Intelligence.WorkItems;

internal sealed class WorkItemAutoResolutionService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory)
{
    public async Task ResolveAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var candidates = await dbContext.WorkItems
            .Where(item => item.UserId == userId && item.ResolvedAt == null)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return;
        }

        var roomIds = candidates
            .Select(item => item.SourceRoom)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var observedFrom = candidates.Min(item => item.ObservedAt);

        var messages = await dbContext.NormalizedMessages
            .AsNoTracking()
            .Where(item => item.UserId == userId &&
                           roomIds.Contains(item.MatrixRoomId))
            .ToListAsync(cancellationToken);

        var messagesByRoom = messages
            .Where(item => item.SentAt >= observedFrom)
            .OrderBy(item => item.SentAt)
            .ThenBy(item => item.IngestedAt)
            .GroupBy(item => item.MatrixRoomId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<NormalizedMessageEntity>)group.ToList(), StringComparer.Ordinal);

        var changed = false;
        foreach (var item in candidates)
        {
            var roomMessages = messagesByRoom.GetValueOrDefault(item.SourceRoom);
            if (roomMessages is null || roomMessages.Count == 0)
            {
                continue;
            }

            var laterMessages = roomMessages
                .Where(message => IsLaterThanItem(message, item))
                .ToList();

            var resolution = WorkItemAutoResolutionDetector.TryResolve(item, laterMessages);
            if (resolution is null)
            {
                continue;
            }

            item.ResolvedAt = resolution.ResolvedAt;
            item.ResolutionKind = resolution.ResolutionKind;
            item.ResolutionSource = resolution.ResolutionSource;
            item.UpdatedAt = resolution.ResolvedAt;
            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static bool IsLaterThanItem(NormalizedMessageEntity message, WorkItemEntity item)
    {
        if (message.SentAt > item.ObservedAt)
        {
            return true;
        }

        return message.SentAt == item.ObservedAt &&
               !string.Equals(message.MatrixEventId, item.SourceEventId, StringComparison.Ordinal);
    }
}
