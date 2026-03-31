using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Features.Search;

namespace SuperChat.Tests;

public sealed class SearchResultViewModelMappingsTests
{
    [Fact]
    public void ToSearchResultViewModel_IncludesAiResolutionNote()
    {
        var result = new WorkItemRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ExtractedItemKind.Commitment,
            "Send deck",
            "Need to send the final deck.",
            "!sales:matrix.localhost",
            "$evt-1",
            null,
            new DateTimeOffset(2026, 03, 16, 09, 00, 00, TimeSpan.Zero),
            null,
            new Confidence(0.88),
            ResolutionSource: "auto_ai_completion",
            ResolutionTrace: new ResolutionTrace(0.93, "deepseek-reasoner", ["$evt-done"]))
            .ToSearchResultViewModel();

        Assert.Equal("AI закрыл как выполненное · 93%", result.ResolutionNote);
        Assert.Equal(0.93, result.ResolutionConfidence);
    }

    [Fact]
    public void ToSearchResultViewModel_LeavesResolutionNoteEmpty_ForUnresolvedItem()
    {
        var result = new WorkItemRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ExtractedItemKind.Task,
            "Need update",
            "Need to return with an update.",
            "!sales:matrix.localhost",
            "$evt-2",
            null,
            new DateTimeOffset(2026, 03, 16, 09, 00, 00, TimeSpan.Zero),
            null,
            new Confidence(0.55))
            .ToSearchResultViewModel();

        Assert.Null(result.ResolutionNote);
        Assert.Null(result.ResolutionConfidence);
    }
}
