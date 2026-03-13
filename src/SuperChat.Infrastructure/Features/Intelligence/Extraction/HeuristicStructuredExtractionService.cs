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

        if (lowered.Contains("meeting") || lowered.Contains("call") || lowered.Contains("встреч") || lowered.Contains("созвон"))
        {
            items.Add(CreateItem(message, ExtractedItemKind.Meeting, "Upcoming meeting", text, dueAt));
        }

        if (lowered.Contains("please send") || lowered.Contains("need to") || lowered.Contains("todo") || lowered.Contains("отправ") || lowered.Contains("нужно"))
        {
            items.Add(CreateItem(message, ExtractedItemKind.Task, "Action needed", text, dueAt));
        }

        if (lowered.Contains("i will") || lowered.Contains("i'll") || lowered.Contains("promise") || lowered.Contains("сделаю") || lowered.Contains("обещаю"))
        {
            items.Add(CreateItem(message, ExtractedItemKind.Commitment, "Commitment made", text, dueAt));
        }

        if (lowered.Contains("waiting for reply") || lowered.Contains("waiting on") || lowered.Contains("respond") || lowered.Contains("жд") || lowered.Contains("ответ"))
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

    private static DateTimeOffset? ResolveDueAt(DateTimeOffset sentAt, string lowered)
    {
        if (lowered.Contains("tomorrow") || lowered.Contains("завтра"))
        {
            return sentAt.Date.AddDays(1).AddHours(10);
        }

        if (lowered.Contains("friday") || lowered.Contains("пятниц"))
        {
            var candidate = sentAt;
            while (candidate.DayOfWeek != DayOfWeek.Friday)
            {
                candidate = candidate.AddDays(1);
            }

            return candidate.Date.AddHours(11);
        }

        if (lowered.Contains("end of day") || lowered.Contains("концу дня"))
        {
            return sentAt.Date.AddHours(18);
        }

        return null;
    }
}
