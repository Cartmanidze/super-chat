using System.Text.RegularExpressions;
using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Services;

public sealed class HeuristicStructuredExtractionService
{
    public Task<IReadOnlyCollection<ExtractedItem>> ExtractAsync(NormalizedMessage message, CancellationToken cancellationToken)
    {
        return ExtractCoreAsync(message, cancellationToken);
    }

    public static Task<IReadOnlyCollection<ExtractedItem>> ExtractCoreAsync(NormalizedMessage message, CancellationToken cancellationToken)
    {
        var items = new List<ExtractedItem>();
        var text = message.Text.Trim();
        var lowered = text.ToLowerInvariant();
        var dueAt = ResolveDueAt(message.SentAt, lowered);

        if (LooksLikeMeeting(lowered, dueAt))
        {
            items.Add(CreateItem(message, ExtractedItemKind.Meeting, "Upcoming meeting", text, dueAt));
        }

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

    private static bool LooksLikeMeeting(string lowered, DateTimeOffset? dueAt)
    {
        if (ContainsAny(lowered, "meeting", "call", "встреч", "созвон", "zoom", "calendar"))
        {
            return true;
        }

        if (dueAt is null)
        {
            return false;
        }

        return ContainsAny(lowered, "заехать", "подъехать", "увид", "встрет", "созвон", "call", "meeting");
    }

    private static DateTimeOffset? ResolveDueAt(DateTimeOffset sentAt, string lowered)
    {
        var explicitTime = TryResolveExplicitTime(sentAt, lowered);
        if (explicitTime is not null)
        {
            return explicitTime;
        }

        if (ContainsAny(lowered, "tomorrow", "завтра"))
        {
            return sentAt.Date.AddDays(1).AddHours(10);
        }

        if (ContainsAny(lowered, "friday", "пятниц"))
        {
            var candidate = sentAt;
            while (candidate.DayOfWeek != DayOfWeek.Friday)
            {
                candidate = candidate.AddDays(1);
            }

            return candidate.Date.AddHours(11);
        }

        if (ContainsAny(lowered, "end of day", "концу дня"))
        {
            return sentAt.Date.AddHours(18);
        }

        return null;
    }

    private static DateTimeOffset? TryResolveExplicitTime(DateTimeOffset sentAt, string lowered)
    {
        var match = Regex.Match(lowered, @"(?:\b(?:at|в)\s*)(?<hour>\d{1,2})(?::(?<minute>\d{2}))?\b");
        if (!match.Success)
        {
            return null;
        }

        var hour = int.Parse(match.Groups["hour"].Value);
        var minute = match.Groups["minute"].Success
            ? int.Parse(match.Groups["minute"].Value)
            : 0;

        if (hour > 23 || minute > 59)
        {
            return null;
        }

        var candidateDate = sentAt.Date;
        if (ContainsAny(lowered, "tomorrow", "завтра"))
        {
            candidateDate = candidateDate.AddDays(1);
        }
        else if (ContainsAny(lowered, "friday", "пятниц"))
        {
            while (candidateDate.DayOfWeek != DayOfWeek.Friday)
            {
                candidateDate = candidateDate.AddDays(1);
            }
        }

        var candidate = candidateDate.AddHours(hour).AddMinutes(minute);
        if (candidate < sentAt.AddMinutes(-15))
        {
            candidate = candidate.AddDays(1);
        }

        return candidate;
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.Ordinal));
    }
}
