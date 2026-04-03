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
        return string.Join(
            '|',
            meeting.SourceRoom,
            meeting.ScheduledFor.UtcDateTime.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            meeting.Summary.Trim().ToLowerInvariant());
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
