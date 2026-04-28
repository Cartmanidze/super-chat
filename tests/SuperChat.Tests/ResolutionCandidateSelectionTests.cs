using SuperChat.Domain.Features.Intelligence;

namespace SuperChat.Tests;

public sealed class ResolutionCandidateSelectionTests
{
    [Fact]
    public void SelectTopCandidates_IncludesOverdueMeetingWithoutMessages()
    {
        var meetingId = Guid.NewGuid();

        var result = ResolutionCandidateSelection.SelectTopCandidates(
        [
            new ResolutionCandidateInput(
                meetingId,
                ExtractedItemKind.Meeting,
                "Созвон",
                "Созвон сегодня в 12:00.",
                null,
                new DateTimeOffset(2026, 03, 16, 10, 00, 00, TimeSpan.Zero),
                new DateTimeOffset(2026, 03, 16, 12, 00, 00, TimeSpan.Zero),
                [])
        ],
            new DateTimeOffset(2026, 03, 16, 13, 00, 00, TimeSpan.Zero),
            5);

        var selected = Assert.Single(result);
        Assert.Equal(meetingId, selected.Id);
        Assert.True(selected.Score > 0d);
    }

}
