using Microsoft.EntityFrameworkCore;
using SuperChat.Domain.Model;
using SuperChat.Domain.Services;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

internal sealed class MeetingUpsertService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory)
{
    public async Task UpsertRangeAsync(IEnumerable<ExtractedItem> items, CancellationToken cancellationToken)
    {
        var meetingItems = items
            .Where(item => item.Kind == ExtractedItemKind.Meeting && item.DueAt is not null)
            .Where(item => !StructuredArtifactDetector.LooksLikeStructuredArtifact(item.Summary))
            .GroupBy(item => (item.UserId, item.SourceEventId))
            .Select(group => group
                .OrderByDescending(item => item.Confidence)
                .ThenByDescending(item => item.ObservedAt)
                .First())
            .ToList();

        if (meetingItems.Count == 0)
        {
            return;
        }

        var userIds = meetingItems.Select(item => item.UserId).Distinct().ToList();
        var sourceEventIds = meetingItems.Select(item => item.SourceEventId).Distinct(StringComparer.Ordinal).ToList();
        var now = DateTimeOffset.UtcNow;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingMeetings = await dbContext.Meetings
            .Where(item => userIds.Contains(item.UserId) && sourceEventIds.Contains(item.SourceEventId))
            .ToListAsync(cancellationToken);

        var existingByKey = existingMeetings.ToDictionary(
            item => BuildKey(item.UserId, item.SourceEventId),
            item => item,
            StringComparer.Ordinal);

        foreach (var item in meetingItems)
        {
            var joinLink = MeetingJoinLinkParser.TryParse(item.Summary);
            var key = BuildKey(item.UserId, item.SourceEventId);
            if (existingByKey.TryGetValue(key, out var existing))
            {
                existing.Title = item.Title;
                existing.Summary = item.Summary;
                existing.SourceRoom = item.SourceRoom;
                existing.Person = item.Person;
                existing.ObservedAt = MeetingTimeSupport.NormalizeToUtc(item.ObservedAt);
                existing.ScheduledFor = MeetingTimeSupport.NormalizeToUtc(item.DueAt!.Value);
                existing.Confidence = item.Confidence;
                existing.MeetingProvider = joinLink?.Provider.ToString();
                existing.MeetingJoinUrl = joinLink?.Url.ToString();
                existing.UpdatedAt = now;
                continue;
            }

            dbContext.Meetings.Add(new MeetingEntity
            {
                Id = item.Id,
                UserId = item.UserId,
                Title = item.Title,
                Summary = item.Summary,
                SourceRoom = item.SourceRoom,
                SourceEventId = item.SourceEventId,
                Person = item.Person,
                ObservedAt = MeetingTimeSupport.NormalizeToUtc(item.ObservedAt),
                ScheduledFor = MeetingTimeSupport.NormalizeToUtc(item.DueAt!.Value),
                Confidence = item.Confidence,
                MeetingProvider = joinLink?.Provider.ToString(),
                MeetingJoinUrl = joinLink?.Url.ToString(),
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string BuildKey(Guid userId, string sourceEventId)
    {
        return $"{userId:N}|{sourceEventId}";
    }
}
