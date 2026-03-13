using SuperChat.Domain.Model;

namespace SuperChat.Domain.Services;

public static class DigestComposer
{
    public static IReadOnlyList<ExtractedItem> BuildToday(IEnumerable<ExtractedItem> items, DateTimeOffset now)
    {
        var dayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        var nextDayStart = dayStart.AddDays(1);

        return items
            .Where(item => item.ObservedAt >= dayStart && item.ObservedAt < nextDayStart)
            .Where(item =>
                item.Kind is ExtractedItemKind.Task or ExtractedItemKind.Commitment ||
                (item.Kind == ExtractedItemKind.Meeting && item.DueAt <= now.AddDays(3)))
            .OrderBy(item => item.DueAt ?? now.AddYears(1))
            .ThenByDescending(item => item.Confidence)
            .Take(10)
            .ToList();
    }

    public static IReadOnlyList<ExtractedItem> BuildWaiting(IEnumerable<ExtractedItem> items)
    {
        return items
            .Where(item => item.Kind == ExtractedItemKind.WaitingOn)
            .OrderByDescending(item => item.ObservedAt)
            .Take(10)
            .ToList();
    }

    public static IReadOnlyList<MeetingRecord> BuildMeetings(IEnumerable<MeetingRecord> items, DateTimeOffset now)
    {
        return items
            .Where(item => item.ScheduledFor >= now.AddHours(-1) && item.ScheduledFor <= now.AddDays(14))
            .OrderBy(item => item.ScheduledFor)
            .ThenByDescending(item => item.Confidence)
            .Take(10)
            .ToList();
    }
}
