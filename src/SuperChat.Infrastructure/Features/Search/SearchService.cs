using SuperChat.Contracts.Features.Intelligence.Retrieval;
using SuperChat.Contracts.Features.Messaging;
using SuperChat.Contracts.Features.Search;
using SuperChat.Contracts.Features.WorkItems;

namespace SuperChat.Infrastructure.Features.Search;

public sealed class SearchService(
    IWorkItemService workItemService,
    IChatMessageStore messageNormalizationService,
    IChatTitleService chatTitleService) : ISearchService
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

            return await ResolveChatTitlesAsync(userId, results, cancellationToken);
        }

        var messages = await messageNormalizationService.SearchRecentMessagesAsync(userId, normalizedQuery, ResultLimit, cancellationToken);
        var messageResults = messages
            .Select(message => message.ToSearchResultViewModel())
            .ToList();

        return await ResolveChatTitlesAsync(userId, messageResults, cancellationToken);
    }

    private async Task<IReadOnlyList<SearchResultViewModel>> ResolveChatTitlesAsync(
        Guid userId,
        IReadOnlyList<SearchResultViewModel> results,
        CancellationToken cancellationToken)
    {
        var chatTitles = await chatTitleService.ResolveManyAsync(userId, results.Select(item => item.ChatTitle), cancellationToken);

        return results
            .Select(result => result.WithResolvedChatTitle(chatTitles))
            .ToList();
    }
}
