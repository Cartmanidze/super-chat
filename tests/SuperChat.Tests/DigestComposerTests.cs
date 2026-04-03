using SuperChat.Domain.Features.Intelligence;

namespace SuperChat.Tests;

public sealed class DigestComposerTests
{
    [Fact]
    public void BuildMeetings_ExcludesMeetingsThatAlreadyStarted()
    {
        var now = new DateTimeOffset(2026, 03, 13, 10, 00, 00, TimeSpan.FromHours(6));
        var meetings = new[]
        {
            new MeetingRecord(Guid.NewGuid(), Guid.NewGuid(), "Upcoming meeting", "Уже идёт", "!team", "$0", null, now.AddMinutes(-30), now.AddMinutes(-10), new Confidence(0.95)),
            new MeetingRecord(Guid.NewGuid(), Guid.NewGuid(), "Upcoming meeting", "Созвон в 11", "!team", "$1", null, now.AddMinutes(-5), now.AddHours(1), new Confidence(0.8)),
            new MeetingRecord(Guid.NewGuid(), Guid.NewGuid(), "Upcoming meeting", "Встреча завтра в 9", "!team", "$2", null, now, now.AddDays(1).AddHours(-1), new Confidence(0.7))
        };

        var upcoming = DigestComposer.BuildMeetings(meetings, now);

        Assert.Equal(2, upcoming.Count);
        Assert.Equal("Созвон в 11", upcoming[0].Summary);
        Assert.Equal("Встреча завтра в 9", upcoming[1].Summary);
        Assert.DoesNotContain(upcoming, item => item.Summary == "Уже идёт");
    }
}
