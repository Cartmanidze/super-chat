using Microsoft.EntityFrameworkCore;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Intelligence.Meetings;

internal sealed class MeetingUpcomingQueryService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    MeetingAutoResolutionService autoResolutionService)
{
    public async Task<IReadOnlyList<MeetingRecord>> GetUpcomingAsync(
        Guid userId,
        DateTimeOffset fromInclusive,
        int take,
        CancellationToken cancellationToken)
    {
        var boundedTake = Math.Clamp(take, 1, 50);
        var utcFromInclusive = MeetingTimeSupport.NormalizeToUtc(fromInclusive);

        await autoResolutionService.ResolveAsync(userId, utcFromInclusive, cancellationToken);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await dbContext.Meetings
            .AsNoTracking()
            .Where(item => item.UserId == userId &&
                           item.ResolvedAt == null)
            .ToListAsync(cancellationToken);

        return entities
            .Where(item => item.ScheduledFor >= utcFromInclusive)
            .OrderBy(item => item.ScheduledFor)
            .ThenByDescending(item => item.Confidence)
            .Take(Math.Max(50, boundedTake * 4))
            .Select(item => item.ToDomain())
            .GroupBy(item => item.ToMeetingDeduplicationKey(), StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(item => item.ScheduledFor)
                .ThenByDescending(item => item.Confidence.Value)
                .ThenByDescending(item => item.ObservedAt)
                .First())
            .OrderBy(item => item.ScheduledFor)
            .ThenByDescending(item => item.Confidence.Value)
            .Take(boundedTake)
            .ToList();
    }
}
