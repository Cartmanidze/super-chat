using SuperChat.Contracts.ViewModels;
using SuperChat.Domain.Services;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.State;

namespace SuperChat.Infrastructure.Services;

public sealed class DigestService(SuperChatStore store) : IDigestService
{
    public Task<IReadOnlyList<DashboardCardViewModel>> GetTodayAsync(Guid userId, CancellationToken cancellationToken)
    {
        var cards = DigestComposer.BuildToday(store.GetExtractedItems(userId), DateTimeOffset.UtcNow)
            .Select(item => new DashboardCardViewModel(item.Title, item.Summary, item.Kind.ToString(), item.DueAt, item.SourceRoom))
            .ToList();

        return Task.FromResult<IReadOnlyList<DashboardCardViewModel>>(cards);
    }

    public Task<IReadOnlyList<DashboardCardViewModel>> GetWaitingAsync(Guid userId, CancellationToken cancellationToken)
    {
        var cards = DigestComposer.BuildWaiting(store.GetExtractedItems(userId))
            .Select(item => new DashboardCardViewModel(item.Title, item.Summary, item.Kind.ToString(), item.DueAt, item.SourceRoom))
            .ToList();

        return Task.FromResult<IReadOnlyList<DashboardCardViewModel>>(cards);
    }
}
