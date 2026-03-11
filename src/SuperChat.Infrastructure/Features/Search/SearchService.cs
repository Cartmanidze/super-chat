using SuperChat.Contracts.ViewModels;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

public sealed class SearchService(
    IExtractedItemService extractedItemService,
    IMessageNormalizationService messageNormalizationService) : ISearchService
{
    public async Task<IReadOnlyList<SearchResultViewModel>> SearchAsync(Guid userId, string query, CancellationToken cancellationToken)
    {
        var normalizedQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return [];
        }

        var extractedItems = await extractedItemService.GetForUserAsync(userId, cancellationToken);
        var results = extractedItems
            .Where(item =>
                item.Title.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                item.Summary.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                item.SourceRoom.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.ObservedAt)
            .Take(20)
            .Select(item => new SearchResultViewModel(item.Title, item.Summary, item.Kind.ToString(), item.SourceRoom, item.ObservedAt))
            .ToList();

        if (results.Count > 0)
        {
            return results;
        }

        var recentMessages = await messageNormalizationService.GetRecentMessagesAsync(userId, 20, cancellationToken);
        return recentMessages
            .Where(message =>
                message.Text.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                message.SenderName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                message.MatrixRoomId.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Select(message => new SearchResultViewModel(message.SenderName, message.Text, "Message", message.MatrixRoomId, message.SentAt))
            .ToList();
    }
}
