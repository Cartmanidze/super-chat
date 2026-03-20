using System.Globalization;
using SuperChat.Contracts;
using SuperChat.Domain.Features.Intelligence;

namespace SuperChat.Infrastructure.Features.Intelligence.Extraction;

internal static class DeepSeekStructuredItemMappings
{
    public static StructuredItemMappingResult ToStructuredItemMappingResult(
        this IReadOnlyList<DeepSeekStructuredItem>? items,
        ConversationWindow window,
        DateTimeOffset sentAtLocal,
        TimeZoneInfo referenceTimeZone)
    {
        var result = new List<ExtractedItem>();
        var unknownKindCount = 0;
        var invalidContentCount = 0;
        if (items is null || items.Count == 0)
        {
            return new StructuredItemMappingResult(result, unknownKindCount, invalidContentCount);
        }

        foreach (var item in items)
        {
            if (!item.Kind.TryToExtractedItemKind(out var kind))
            {
                unknownKindCount++;
                continue;
            }

            var summary = string.IsNullOrWhiteSpace(item.Summary)
                ? window.Transcript
                : item.Summary.Trim();

            var title = string.IsNullOrWhiteSpace(item.Title)
                ? kind.ToDefaultStructuredItemTitle(item.Person)
                : item.Title.Trim();

            if (string.IsNullOrWhiteSpace(summary) || string.IsNullOrWhiteSpace(title))
            {
                invalidContentCount++;
                continue;
            }

            var confidence = Math.Clamp(item.Confidence ?? 0.74d, 0d, 1d);
            var dueAt = item.Deadline.ParseStructuredDeadline(sentAtLocal, referenceTimeZone);
            var person = string.IsNullOrWhiteSpace(item.Person) ? null : item.Person.Trim();

            result.Add(new ExtractedItem(
                Guid.NewGuid(),
                window.UserId,
                kind,
                title,
                summary,
                window.MatrixRoomId,
                window.LastMessage.MatrixEventId,
                person,
                window.TsTo,
                dueAt,
                confidence));
        }

        return new StructuredItemMappingResult(result, unknownKindCount, invalidContentCount);
    }

    public static void MergeInto(
        this MeetingSignal deterministicMeeting,
        List<ExtractedItem> extractedItems,
        ConversationWindow window)
    {
        var existingMeetingIndex = extractedItems.FindIndex(item => item.Kind == ExtractedItemKind.Meeting);
        if (existingMeetingIndex < 0)
        {
            extractedItems.Add(new ExtractedItem(
                Guid.NewGuid(),
                window.UserId,
                ExtractedItemKind.Meeting,
                deterministicMeeting.Title,
                deterministicMeeting.Summary,
                window.MatrixRoomId,
                window.LastMessage.MatrixEventId,
                deterministicMeeting.Person,
                window.TsTo,
                deterministicMeeting.ScheduledFor,
                deterministicMeeting.Confidence));
            return;
        }

        var existingMeeting = extractedItems[existingMeetingIndex];
        extractedItems[existingMeetingIndex] = existingMeeting with
        {
            Title = deterministicMeeting.Title,
            Summary = deterministicMeeting.Summary,
            Person = deterministicMeeting.Person ?? existingMeeting.Person,
            ObservedAt = window.TsTo,
            DueAt = deterministicMeeting.ScheduledFor,
            Confidence = Math.Max(existingMeeting.Confidence, deterministicMeeting.Confidence)
        };
    }

    public static bool TryToExtractedItemKind(this string? kind, out ExtractedItemKind extractedItemKind)
    {
        var normalized = kind?.Trim().ToLowerInvariant();
        extractedItemKind = normalized switch
        {
            "task" or "todo" or "action" => ExtractedItemKind.Task,
            "commitment" or "promise" => ExtractedItemKind.Commitment,
            "waiting_on" or "waitingon" or "waiting" or "follow_up" => ExtractedItemKind.WaitingOn,
            "meeting" or "call" => ExtractedItemKind.Meeting,
            _ => default
        };

        return normalized is "task" or "todo" or "action" or
            "commitment" or "promise" or
            "waiting_on" or "waitingon" or "waiting" or "follow_up" or
            "meeting" or "call";
    }

    public static string ToDefaultStructuredItemTitle(this ExtractedItemKind kind, string? person)
    {
        return kind switch
        {
            ExtractedItemKind.WaitingOn when !string.IsNullOrWhiteSpace(person) => $"Нужно ответить: {person!.Trim()}",
            ExtractedItemKind.WaitingOn => "Нужно ответить",
            ExtractedItemKind.Commitment => "Ты пообещал",
            ExtractedItemKind.Task => "Нужен следующий шаг",
            ExtractedItemKind.Meeting => "Скоро встреча",
            _ => "Важный сигнал"
        };
    }

    public static DateTimeOffset? ParseStructuredDeadline(
        this string? value,
        DateTimeOffset sentAtLocal,
        TimeZoneInfo referenceTimeZone)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (DateTimeOffset.TryParse(
                normalized,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsedOffset))
        {
            return parsedOffset.ToUniversalTime();
        }

        if (!DateTime.TryParse(
                normalized,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsedDateTime))
        {
            return null;
        }

        var unspecifiedLocal = DateTime.SpecifyKind(parsedDateTime, DateTimeKind.Unspecified);
        var offset = referenceTimeZone.GetUtcOffset(unspecifiedLocal);
        var localDeadline = new DateTimeOffset(unspecifiedLocal, offset);

        if (localDeadline < sentAtLocal.AddDays(-1))
        {
            return null;
        }

        return localDeadline.ToUniversalTime();
    }
}

internal sealed record StructuredItemMappingResult(
    List<ExtractedItem> Items,
    int UnknownKindCount,
    int InvalidContentCount)
{
    public string MappedKinds => Items.Count == 0
        ? "none"
        : string.Join(",", Items.Select(item => item.Kind.ToString()));
}
