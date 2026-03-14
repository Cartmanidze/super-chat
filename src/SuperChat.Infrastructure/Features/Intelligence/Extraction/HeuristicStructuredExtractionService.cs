using System.Text.RegularExpressions;
using SuperChat.Contracts.Configuration;
using SuperChat.Domain.Model;
using SuperChat.Domain.Services;

namespace SuperChat.Infrastructure.Services;

public sealed class HeuristicStructuredExtractionService(PilotOptions pilotOptions)
{
    public Task<IReadOnlyCollection<ExtractedItem>> ExtractAsync(NormalizedMessage message, CancellationToken cancellationToken)
    {
        return ExtractCoreAsync(message, ResolveReferenceTimeZone(pilotOptions.TodayTimeZoneId), cancellationToken);
    }

    public static Task<IReadOnlyCollection<ExtractedItem>> ExtractCoreAsync(
        NormalizedMessage message,
        TimeZoneInfo referenceTimeZone,
        CancellationToken cancellationToken)
    {
        var items = new List<ExtractedItem>();
        var text = message.Text.Trim();
        if (StructuredArtifactDetector.LooksLikeStructuredArtifact(text))
        {
            return Task.FromResult<IReadOnlyCollection<ExtractedItem>>(Array.Empty<ExtractedItem>());
        }

        var lowered = text.ToLowerInvariant();

        var meetingSignal = MeetingSignalDetector.TryFromMessage(message, referenceTimeZone);
        if (meetingSignal is not null)
        {
            items.Add(new ExtractedItem(
                Guid.NewGuid(),
                message.UserId,
                ExtractedItemKind.Meeting,
                meetingSignal.Title,
                meetingSignal.Summary,
                message.MatrixRoomId,
                message.MatrixEventId,
                meetingSignal.Person,
                message.SentAt,
                meetingSignal.ScheduledFor,
                meetingSignal.Confidence));
        }

        var dueAt = meetingSignal?.ScheduledFor;
        if (ContainsAny(lowered, "please send", "need to", "todo", "отправ", "нужно"))
        {
            items.Add(CreateItem(message, ExtractedItemKind.Task, "Action needed", text, dueAt));
        }

        if (ContainsAny(lowered, "i will", "i'll", "promise", "сделаю", "обещаю"))
        {
            items.Add(CreateItem(message, ExtractedItemKind.Commitment, "Commitment made", text, dueAt));
        }

        if (ContainsAny(lowered, "waiting for reply", "waiting on", "respond", "жд", "ответ"))
        {
            items.Add(CreateItem(message, ExtractedItemKind.WaitingOn, "Awaiting response", text, dueAt));
        }

        return Task.FromResult<IReadOnlyCollection<ExtractedItem>>(items);
    }

    private static ExtractedItem CreateItem(
        NormalizedMessage message,
        ExtractedItemKind kind,
        string title,
        string summary,
        DateTimeOffset? dueAt,
        double confidence = 0.82)
    {
        var person = Regex.Match(summary, @"\b[A-Z][a-z]+\b").Value;

        return new ExtractedItem(
            Guid.NewGuid(),
            message.UserId,
            kind,
            title,
            summary,
            message.MatrixRoomId,
            message.MatrixEventId,
            string.IsNullOrWhiteSpace(person) ? null : person,
            message.SentAt,
            dueAt,
            confidence);
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.Ordinal));
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
