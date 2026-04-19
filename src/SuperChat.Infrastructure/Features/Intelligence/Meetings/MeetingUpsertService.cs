using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Persistence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Infrastructure.Features.Intelligence.Meetings;

internal sealed class MeetingUpsertService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    ILogger<MeetingUpsertService> logger)
{
    private static readonly string[] SameLinkKeywords =
    [
        "same link", "link same",
        "ссылка та же", "ссылка всё та же", "ссылка все та же"
    ];

    public async Task UpsertRangeAsync(IEnumerable<ExtractedItem> items, CancellationToken cancellationToken)
    {
        var meetingItems = items
            .Where(item => item.Kind == ExtractedItemKind.Meeting)
            .Where(item => !StructuredArtifactDetector.LooksLikeStructuredArtifact(item.Summary))
            .GroupBy(item => (item.UserId, item.SourceEventId))
            .Select(group => group
                .OrderByDescending(item => item.Confidence.Value)
                .ThenByDescending(item => item.ObservedAt)
                .First())
            .ToList();

        if (meetingItems.Count == 0)
        {
            return;
        }

        var userIds = meetingItems.Select(item => item.UserId).Distinct().ToList();
        var sourceEventIds = meetingItems.Select(item => item.SourceEventId).Distinct(StringComparer.Ordinal).ToList();
        var sourceRooms = meetingItems.Select(item => item.SourceRoom).Distinct(StringComparer.Ordinal).ToList();
        var now = DateTimeOffset.UtcNow;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingMeetings = await dbContext.Meetings
            .Where(item => userIds.Contains(item.UserId) && sourceEventIds.Contains(item.SourceEventId))
            .ToListAsync(cancellationToken);
        var unresolvedRoomMeetings = await dbContext.Meetings
            .Where(item => userIds.Contains(item.UserId) &&
                           sourceRooms.Contains(item.SourceRoom) &&
                           item.ResolvedAt == null)
            .ToListAsync(cancellationToken);
        var sourceMessageTexts = await dbContext.NormalizedMessages
            .Where(item => userIds.Contains(item.UserId) && sourceEventIds.Contains(item.ExternalMessageId))
            .Select(item => new
            {
                item.UserId,
                SourceEventId = item.ExternalMessageId,
                item.Text
            })
            .ToListAsync(cancellationToken);

        var existingByKey = existingMeetings.ToDictionary(
            item => BuildKey(item.UserId, item.SourceEventId),
            item => item,
            StringComparer.Ordinal);
        var existingByDedupKey = existingMeetings
            .Concat(unresolvedRoomMeetings)
            .GroupBy(item => item.ToMeetingDeduplicationKey(), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => MeetingRecordMappings.SelectDedupPriorityMeeting(group),
                StringComparer.Ordinal);
        var sourceTextByKey = sourceMessageTexts
            .GroupBy(item => BuildKey(item.UserId, item.SourceEventId), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Last().Text,
                StringComparer.Ordinal);

        foreach (var item in meetingItems)
        {
            var key = BuildKey(item.UserId, item.SourceEventId);
            var joinLink = MeetingJoinLinkParser.TryParse(item.Summary);
            if (joinLink is null &&
                sourceTextByKey.TryGetValue(key, out var sourceText))
            {
                joinLink = MeetingJoinLinkParser.TryParse(sourceText);
            }

            var status = WorkItemPresentationMetadata.ResolveMeetingStatus(item.Summary);
            if (existingByKey.TryGetValue(key, out var existing))
            {
                var previousDedupKey = existing.ToMeetingDeduplicationKey();
                UpdateMeeting(existing, item, status, joinLink, now);
                RefreshDedupIndex(existingByDedupKey, existing, previousDedupKey);
                continue;
            }

            var correlatedMeeting = TryFindCorrelatedMeeting(
                item,
                status,
                joinLink,
                sourceTextByKey.GetValueOrDefault(key),
                unresolvedRoomMeetings);
            if (correlatedMeeting is not null)
            {
                var previousKey = BuildKey(correlatedMeeting.UserId, correlatedMeeting.SourceEventId);
                var previousDedupKey = correlatedMeeting.ToMeetingDeduplicationKey();
                UpdateMeeting(correlatedMeeting, item, status, joinLink, now);
                correlatedMeeting.SourceEventId = item.SourceEventId;
                existingByKey.Remove(previousKey);
                existingByKey[BuildKey(correlatedMeeting.UserId, correlatedMeeting.SourceEventId)] = correlatedMeeting;
                RefreshDedupIndex(existingByDedupKey, correlatedMeeting, previousDedupKey);
                continue;
            }

            if (item.DueAt is null)
            {
                logger.LogInformation(
                    "Skipping meeting upsert — no DueAt and no correlated meeting. ItemId={ItemId}, UserId={UserId}, SourceEventId={SourceEventId}, SourceRoom={SourceRoom}.",
                    item.Id,
                    item.UserId,
                    item.SourceEventId,
                    item.SourceRoom);
                continue;
            }

            var dedupKey = BuildDedupKey(item);
            if (existingByDedupKey.TryGetValue(dedupKey, out var duplicate))
            {
                var previousDedupKey = duplicate.ToMeetingDeduplicationKey();
                UpdateMeeting(duplicate, item, status, joinLink, now);
                RefreshDedupIndex(existingByDedupKey, duplicate, previousDedupKey);
                continue;
            }

            var newEntity = new MeetingEntity
            {
                Id = item.Id,
                UserId = item.UserId,
                Title = item.Title,
                Summary = item.Summary,
                SourceRoom = item.SourceRoom,
                SourceEventId = item.SourceEventId,
                Person = item.Person,
                ObservedAt = MeetingTimeSupport.NormalizeToUtc(item.ObservedAt),
                ScheduledFor = MeetingTimeSupport.NormalizeToUtc(item.DueAt),
                Confidence = item.Confidence,
                Status = status,
                MeetingProvider = joinLink?.Provider.ToString(),
                MeetingJoinUrl = joinLink?.Url.ToString(),
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.Meetings.Add(newEntity);
            existingByDedupKey[dedupKey] = newEntity;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void UpdateMeeting(
        MeetingEntity existing,
        ExtractedItem item,
        MeetingStatus status,
        MeetingJoinLink? joinLink,
        DateTimeOffset now)
    {
        existing.Title = item.Title;
        existing.Summary = item.Summary;
        existing.SourceRoom = item.SourceRoom;
        existing.Person = item.Person;
        existing.ObservedAt = MeetingTimeSupport.NormalizeToUtc(item.ObservedAt);
        existing.ScheduledFor = MeetingTimeSupport.NormalizeToUtc(item.DueAt);
        existing.Confidence = item.Confidence;
        existing.Status = status;
        existing.MeetingProvider = joinLink?.Provider.ToString();
        existing.MeetingJoinUrl = joinLink?.Url.ToString();
        existing.UpdatedAt = now;
    }

    private static MeetingEntity? TryFindCorrelatedMeeting(
        ExtractedItem item,
        MeetingStatus status,
        MeetingJoinLink? joinLink,
        string? sourceText,
        IReadOnlyList<MeetingEntity> unresolvedRoomMeetings)
    {
        if (status != MeetingStatus.Rescheduled)
        {
            return null;
        }

        var roomCandidates = unresolvedRoomMeetings
            .Where(candidate => candidate.UserId == item.UserId &&
                                candidate.SourceRoom == item.SourceRoom &&
                                !string.Equals(candidate.SourceEventId, item.SourceEventId, StringComparison.Ordinal))
            .OrderByDescending(candidate => candidate.ObservedAt)
            .ToList();
        if (roomCandidates.Count == 0)
        {
            return null;
        }

        if (joinLink is not null)
        {
            var byLink = roomCandidates
                .Where(candidate => string.Equals(
                    candidate.MeetingJoinUrl,
                    joinLink.Url.ToString(),
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (byLink.Count == 1)
            {
                return byLink[0];
            }
        }

        if (item.DueAt is null && roomCandidates.Count == 1)
        {
            return roomCandidates[0];
        }

        if (ContainsSameLinkCue(sourceText ?? item.Summary) && roomCandidates.Count == 1)
        {
            return roomCandidates[0];
        }

        return null;
    }

    private static string BuildKey(Guid userId, string sourceEventId)
    {
        return $"{userId:N}|{sourceEventId}";
    }

    private static string BuildDedupKey(ExtractedItem item)
    {
        return MeetingRecordMappings.BuildDeduplicationKey(
            item.SourceRoom,
            MeetingTimeSupport.NormalizeToUtc(item.DueAt)!.Value.UtcDateTime,
            item.Summary);
    }

    private static void RefreshDedupIndex(
        IDictionary<string, MeetingEntity> existingByDedupKey,
        MeetingEntity entity,
        string previousDedupKey)
    {
        if (existingByDedupKey.TryGetValue(previousDedupKey, out var mappedEntity) &&
            ReferenceEquals(mappedEntity, entity))
        {
            existingByDedupKey.Remove(previousDedupKey);
        }

        existingByDedupKey[entity.ToMeetingDeduplicationKey()] = entity;
    }

    private static bool ContainsSameLinkCue(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var lowered = text.Trim().ToLowerInvariant();
        return SameLinkKeywords.Any(keyword => lowered.Contains(keyword, StringComparison.Ordinal));
    }
}
