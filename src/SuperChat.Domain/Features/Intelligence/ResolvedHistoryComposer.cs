namespace SuperChat.Domain.Features.Intelligence;

public static class ResolvedHistoryComposer
{
    private static readonly TimeSpan RecentWindow = TimeSpan.FromDays(2);

    public static IReadOnlyList<WorkItemRecord> BuildRecentAutoResolved(
        IReadOnlyList<WorkItemRecord> items,
        DateTimeOffset now,
        int take = 6)
    {
        return items
            .Where(item => item.ResolvedAt is not null &&
                           item.ResolutionSource?.StartsWith("auto", StringComparison.OrdinalIgnoreCase) == true)
            .Where(item => item.ResolvedAt >= now - RecentWindow)
            .OrderByDescending(item => item.ResolvedAt)
            .Take(Math.Max(1, take))
            .ToList();
    }
}
