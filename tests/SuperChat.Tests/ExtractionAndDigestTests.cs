using SuperChat.Domain.Model;
using SuperChat.Domain.Services;
using SuperChat.Infrastructure.Services;

namespace SuperChat.Tests;

public sealed class ExtractionAndDigestTests
{
    [Fact]
    public async Task HeuristicExtraction_RecognizesTaskAndWaiting()
    {
        var message = new NormalizedMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "telegram",
            "!sales:matrix.localhost",
            "$event-1",
            "Alex",
            "Please send the proposal tomorrow. Still waiting for reply from Marina.",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            false);

        var service = new HeuristicStructuredExtractionService();
        var items = await service.ExtractAsync(message, CancellationToken.None);

        Assert.Contains(items, item => item.Kind == ExtractedItemKind.Task);
        Assert.Contains(items, item => item.Kind == ExtractedItemKind.WaitingOn);
    }

    [Fact]
    public void DigestComposer_PrioritizesWaitingAndTodayItems()
    {
        var now = DateTimeOffset.UtcNow;
        var items = new[]
        {
            new ExtractedItem(Guid.NewGuid(), Guid.NewGuid(), ExtractedItemKind.Task, "Send contract", "send contract", "!ops", "$1", null, now, now.AddHours(6), 0.9),
            new ExtractedItem(Guid.NewGuid(), Guid.NewGuid(), ExtractedItemKind.WaitingOn, "Waiting on Marina", "waiting", "!sales", "$2", "Marina", now, null, 0.88),
            new ExtractedItem(Guid.NewGuid(), Guid.NewGuid(), ExtractedItemKind.Meeting, "Friday sync", "meeting", "!team", "$3", null, now, now.AddDays(1), 0.77)
        };

        var today = DigestComposer.BuildToday(items, now);
        var waiting = DigestComposer.BuildWaiting(items);

        Assert.Equal(2, today.Count);
        Assert.Single(waiting);
        Assert.Equal("Waiting on Marina", waiting[0].Title);
    }
}
