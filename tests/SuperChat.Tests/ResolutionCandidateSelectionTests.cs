using SuperChat.Domain.Features.Intelligence;

namespace SuperChat.Tests;

public sealed class ResolutionCandidateSelectionTests
{
    [Fact]
    public void SelectTopCandidates_PrioritizesCompletionSignals()
    {
        var strongId = Guid.NewGuid();
        var weakId = Guid.NewGuid();

        var result = ResolutionCandidateSelection.SelectTopCandidates(
        [
            new ResolutionCandidateInput(
                weakId,
                ExtractedItemKind.Task,
                "Нужен апдейт по договору",
                "Вернуться с апдейтом.",
                null,
                new DateTimeOffset(2026, 03, 16, 09, 00, 00, TimeSpan.Zero),
                null,
                [
                    new ResolutionEvidenceMessageInput("Alex", "ок, спасибо", new DateTimeOffset(2026, 03, 16, 09, 05, 00, TimeSpan.Zero))
                ]),
            new ResolutionCandidateInput(
                strongId,
                ExtractedItemKind.Commitment,
                "Отправить финальный дек",
                "Нужно отправить финальный дек клиенту.",
                null,
                new DateTimeOffset(2026, 03, 16, 09, 00, 00, TimeSpan.Zero),
                null,
                [
                    new ResolutionEvidenceMessageInput("You", "готово, отправил финальный дек клиенту", new DateTimeOffset(2026, 03, 16, 09, 07, 00, TimeSpan.Zero))
                ])
        ],
            new DateTimeOffset(2026, 03, 16, 09, 10, 00, TimeSpan.Zero),
            1);

        var top = Assert.Single(result);
        Assert.Equal(strongId, top.Id);
    }

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

    [Fact]
    public void SelectTopCandidates_ExcludesWeakNonMeetingCandidatesWithoutEvidence()
    {
        var result = ResolutionCandidateSelection.SelectTopCandidates(
        [
            new ResolutionCandidateInput(
                Guid.NewGuid(),
                ExtractedItemKind.Task,
                "Нужен следующий шаг",
                "Нужно вернуться позже.",
                null,
                new DateTimeOffset(2026, 03, 16, 09, 00, 00, TimeSpan.Zero),
                null,
                [])
        ],
            new DateTimeOffset(2026, 03, 16, 13, 00, 00, TimeSpan.Zero),
            5);

        Assert.Empty(result);
    }
}
