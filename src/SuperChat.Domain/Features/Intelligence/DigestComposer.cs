using SuperChat.Domain.Model;

namespace SuperChat.Domain.Services;

public static class DigestComposer
{
    public static IReadOnlyList<ExtractedItem> BuildToday(IEnumerable<ExtractedItem> items, DateTimeOffset now)
    {
        return items
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
}
