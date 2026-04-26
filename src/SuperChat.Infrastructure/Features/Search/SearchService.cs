using SuperChat.Contracts.Features.Intelligence.Retrieval;
using SuperChat.Contracts.Features.Messaging;
using SuperChat.Contracts.Features.Search;
using SuperChat.Contracts.Features.WorkItems;

namespace SuperChat.Infrastructure.Features.Search;

public sealed class SearchService(
    IWorkItemService workItemService,
    IMessageNormalizationService messageNormalizationService,
    IRoomDisplayNameService roomDisplayNameService) : ISearchService
{
    private const int ResultLimit = 20;

    public async Task<IReadOnlyList<SearchResultViewModel>> SearchAsync(Guid userId, string query, CancellationToken cancellationToken)
    {
        var normalizedQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return [];
        }

        var workItems = await workItemService.SearchAsync(userId, normalizedQuery, ResultLimit, cancellationToken);
        if (workItems.Count > 0)
        {
            var results = workItems
                .Select(item => item.ToSearchResultViewModel())
                .ToList();

            return await ResolveRoomNamesAsync(userId, results, cancellationToken);
        }

        var messages = await messageNormalizationService.SearchRecentMessagesAsync(userId, normalizedQuery, ResultLimit, cancellationToken);
        var messageResults = messages
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
