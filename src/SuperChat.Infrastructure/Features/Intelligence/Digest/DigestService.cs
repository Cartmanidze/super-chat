using SuperChat.Contracts.ViewModels;
using SuperChat.Domain.Services;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

public sealed class DigestService(IExtractedItemService extractedItemService) : IDigestService
{
    public async Task<IReadOnlyList<DashboardCardViewModel>> GetTodayAsync(Guid userId, CancellationToken cancellationToken)
    {
        var items = await extractedItemService.GetForUserAsync(userId, cancellationToken);
        var cards = DigestComposer.BuildToday(items, DateTimeOffset.UtcNow)
            .Select(item => new DashboardCardViewModel(item.Title, item.Summary, item.Kind.ToString(), item.DueAt, item.SourceRoom))
            .ToList();

        return cards;
    }

    public async Task<IReadOnlyList<DashboardCardViewModel>> GetWaitingAsync(Guid userId, CancellationToken cancellationToken)
    {
        var items = await extractedItemService.GetForUserAsync(userId, cancellationToken);
        var cards = DigestComposer.BuildWaiting(items)
            .Select(item => new DashboardCardViewModel(item.Title, item.Summary, item.Kind.ToString(), item.DueAt, item.SourceRoom))
            .ToList();

        return cards;
    }
}
