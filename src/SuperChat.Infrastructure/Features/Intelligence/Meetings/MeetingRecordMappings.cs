using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Persistence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Infrastructure.Features.Intelligence.Meetings;

internal static class MeetingRecordMappings
{
    public static MeetingRecord? ToMeetingCandidate(this MessageChunkEntity chunk, TimeZoneInfo referenceTimeZone)
    {
        if (StructuredArtifactDetector.LooksLikeStructuredArtifact(chunk.Text))
        {
            return null;
        }

        var signal = MeetingSignalDetector.TryFromChunk(
            chunk.Text,
            chunk.TsTo,
            chunk.TsTo,
            referenceTimeZone);

        if (signal is null)
        {
            return null;
        }

        var joinLink = MeetingJoinLinkParser.TryParse(chunk.Text);
        return new MeetingRecord(
            chunk.ContentHash.ToDeterministicGuid(),
            chunk.UserId,
            signal.Title,
            signal.Summary,
            chunk.ChatId,
            chunk.ToChunkSourceEventId(),
            signal.Person,
            signal.ObservedAt,
            signal.ScheduledFor,
            signal.Confidence,
            MeetingProvider: joinLink?.Provider.ToString(),
            MeetingJoinUrl: joinLink?.Url,
            Status: WorkItemPresentationMetadata.ResolveMeetingStatus(signal.Summary));
    }

    public static string ToChunkSourceEventId(this MessageChunkEntity chunk)
    {
        return $"chunk:{chunk.ContentHash}";
    }

    public static string ToMeetingDeduplicationKey(this MeetingRecord meeting)
    {
        return meeting.ScheduledFor is DateTimeOffset scheduledFor
            ? BuildDeduplicationKey(
                meeting.ExternalChatId,
                scheduledFor.UtcDateTime,
                meeting.Summary)
            : BuildUnscheduledDeduplicationKey(
                meeting.ExternalChatId,
                meeting.SourceEventId,
                meeting.Summary);
    }

    public static string ToMeetingDeduplicationKey(this MeetingEntity entity)
    {
        return entity.ScheduledFor is DateTimeOffset scheduledFor
            ? BuildDeduplicationKey(
                entity.ExternalChatId,
                scheduledFor.UtcDateTime,
                entity.Summary)
            : BuildUnscheduledDeduplicationKey(
                entity.ExternalChatId,
                entity.SourceEventId,
                entity.Summary);
    }

    internal static string BuildDeduplicationKey(string externalChatId, DateTime scheduledForUtc, string summary)
    {
        return string.Join(
            '|',
            externalChatId,
            scheduledForUtc.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            summary.Trim().ToLowerInvariant());
    }

    internal static string BuildUnscheduledDeduplicationKey(string externalChatId, string sourceEventId, string summary)
    {
        return string.Join(
            '|',
            externalChatId,
            "unscheduled",
            sourceEventId,
            summary.Trim().ToLowerInvariant());
    }

    internal static MeetingEntity SelectDedupPriorityMeeting(IEnumerable<MeetingEntity> group)
    {
        return group
            .OrderBy(item => item.SourceEventId.StartsWith("chunk:", StringComparison.Ordinal) ? 1 : 0)
            .ThenBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .First();
    }

    public static Guid ToDeterministicGuid(this string seed)
    {
        Span<byte> bytes = stackalloc byte[16];
        var sourceBytes = Encoding.UTF8.GetBytes(seed);
        var hash = SHA256.HashData(sourceBytes);
        hash[..16].CopyTo(bytes);
        return new Guid(bytes);
    }
}
