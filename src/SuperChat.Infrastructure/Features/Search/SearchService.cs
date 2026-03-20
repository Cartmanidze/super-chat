using SuperChat.Contracts.Features.Search;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Features.Messaging;

namespace SuperChat.Infrastructure.Features.Search;

public sealed class SearchService(
    IWorkItemService workItemService,
    IMessageNormalizationService messageNormalizationService,
    IRoomDisplayNameService roomDisplayNameService) : ISearchService
{
    public async Task<IReadOnlyList<SearchResultViewModel>> SearchAsync(Guid userId, string query, CancellationToken cancellationToken)
    {
        var normalizedQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return [];
        }

        var workItems = await workItemService.GetForUserAsync(userId, cancellationToken);
        var results = workItems
            .Where(item =>
                item.Title.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                item.Summary.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                item.SourceRoom.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.ObservedAt)
            .Take(20)
            .Select(item => item.ToSearchResultViewModel())
            .ToList();

        if (results.Count > 0)
        {
            return await ResolveRoomNamesAsync(userId, results, cancellationToken);
        }

        var recentMessages = await messageNormalizationService.GetRecentMessagesAsync(userId, 20, cancellationToken);
        var messageResults = recentMessages
            .Where(message =>
                message.Text.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                message.SenderName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                message.MatrixRoomId.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Select(message => message.ToSearchResultViewModel())
            .ToList();

        return await ResolveRoomNamesAsync(userId, messageResults, cancellationToken);
    }

    private async Task<IReadOnlyList<SearchResultViewModel>> ResolveRoomNamesAsync(
        Guid userId,
        IReadOnlyList<SearchResultViewModel> results,
        CancellationToken cancellationToken)
    {
        var roomNames = await roomDisplayNameService.ResolveManyAsync(userId, results.Select(item => item.SourceRoom), cancellationToken);

        return results
            .Select(result => result.WithResolvedSourceRoom(roomNames))
            .ToList();
    }
}
