using Microsoft.EntityFrameworkCore;
using SuperChat.Contracts.Configuration;
using SuperChat.Domain.Model;
using SuperChat.Domain.Services;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

public sealed class MeetingService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    PilotOptions pilotOptions) : IMeetingService
{
    public async Task UpsertRangeAsync(IEnumerable<ExtractedItem> items, CancellationToken cancellationToken)
    {
        var meetingItems = items
            .Where(item => item.Kind == ExtractedItemKind.Meeting && item.DueAt is not null)
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
            var key = BuildKey(item.UserId, item.SourceEventId);
            if (existingByKey.TryGetValue(key, out var existing))
            {
                existing.Title = item.Title;
                existing.Summary = item.Summary;
                existing.SourceRoom = item.SourceRoom;
                existing.Person = item.Person;
                existing.ObservedAt = NormalizeToUtc(item.ObservedAt);
                existing.ScheduledFor = NormalizeToUtc(item.DueAt!.Value);
                existing.Confidence = item.Confidence;
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
                ObservedAt = NormalizeToUtc(item.ObservedAt),
                ScheduledFor = NormalizeToUtc(item.DueAt!.Value),
                Confidence = item.Confidence,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MeetingRecord>> GetUpcomingAsync(
        Guid userId,
        DateTimeOffset fromInclusive,
        int take,
        CancellationToken cancellationToken)
    {
        var boundedTake = Math.Clamp(take, 1, 50);
        var utcFromInclusive = NormalizeToUtc(fromInclusive);
        var referenceTimeZone = ResolveReferenceTimeZone(pilotOptions.TodayTimeZoneId);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await dbContext.Meetings
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.ScheduledFor >= utcFromInclusive)
            .OrderBy(item => item.ScheduledFor)
            .ThenByDescending(item => item.Confidence)
            .Take(boundedTake)
            .ToListAsync(cancellationToken);

        var chunkCandidates = await dbContext.MessageChunks
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Where(item => item.TsTo >= utcFromInclusive.AddDays(-2) && item.TsFrom <= utcFromInclusive.AddDays(14))
            .OrderByDescending(item => item.TsTo)
            .Take(500)
            .ToListAsync(cancellationToken);

        return entities
            .Select(item => item.ToDomain())
            .Concat(chunkCandidates
                .Select(item => ToMeetingCandidate(item, referenceTimeZone))
                .OfType<MeetingRecord>())
            .Where(item => item.ScheduledFor >= utcFromInclusive)
            .GroupBy(BuildMeetingDeduplicationKey, StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(item => item.ScheduledFor)
                .ThenByDescending(item => item.Confidence)
                .ThenByDescending(item => item.ObservedAt)
                .First())
            .OrderBy(item => item.ScheduledFor)
            .ThenByDescending(item => item.Confidence)
            .Take(boundedTake)
            .ToList();
    }

    private static string BuildKey(Guid userId, string sourceEventId)
    {
        return $"{userId:N}|{sourceEventId}";
    }

    private static DateTimeOffset NormalizeToUtc(DateTimeOffset value)
    {
        return value.Offset == TimeSpan.Zero
            ? value
            : value.ToUniversalTime();
    }

    private static MeetingRecord? ToMeetingCandidate(MessageChunkEntity chunk, TimeZoneInfo referenceTimeZone)
    {
        var signal = MeetingSignalDetector.TryFromChunk(
            chunk.Text,
            chunk.TsTo,
            chunk.TsTo,
            referenceTimeZone);

        if (signal is null)
        {
            return null;
        }

        return new MeetingRecord(
            BuildDeterministicGuid(chunk.ContentHash),
            chunk.UserId,
            signal.Title,
            signal.Summary,
            chunk.ChatId,
            $"chunk:{chunk.ContentHash}",
            signal.Person,
            signal.ObservedAt,
            signal.ScheduledFor,
            signal.Confidence);
    }

    private static string BuildMeetingDeduplicationKey(MeetingRecord meeting)
    {
        return string.Join(
            '|',
            meeting.SourceRoom,
            meeting.ScheduledFor.UtcDateTime.ToString("yyyy-MM-ddTHH:mm", System.Globalization.CultureInfo.InvariantCulture),
            meeting.Summary.Trim().ToLowerInvariant());
    }

    private static Guid BuildDeterministicGuid(string seed)
    {
        Span<byte> bytes = stackalloc byte[16];
        var sourceBytes = System.Text.Encoding.UTF8.GetBytes(seed);
        var hash = System.Security.Cryptography.SHA256.HashData(sourceBytes);
        hash[..16].CopyTo(bytes);
        return new Guid(bytes);
    }

    private static TimeZoneInfo ResolveReferenceTimeZone(string configuredTimeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(configuredTimeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(configuredTimeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}
