namespace SuperChat.Domain.Features.Intelligence;

public static class DigestComposer
{
    public static IReadOnlyList<MeetingRecord> BuildMeetings(IEnumerable<MeetingRecord> items, DateTimeOffset now)
    {
        return items
            .Where(item => item.ScheduledFor >= now && item.ScheduledFor <= now.AddDays(14))
            .OrderBy(item => item.ScheduledFor)
            .ThenByDescending(item => item.Confidence.Value)
            .Take(10)
            .ToList();
    }
}
