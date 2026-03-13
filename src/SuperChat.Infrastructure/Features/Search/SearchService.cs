using SuperChat.Contracts.ViewModels;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

public sealed class SearchService(
    IExtractedItemService extractedItemService,
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
            return await ResolveRoomNamesAsync(userId, results, cancellationToken);
        }

        var recentMessages = await messageNormalizationService.GetRecentMessagesAsync(userId, 20, cancellationToken);
        var messageResults = recentMessages
            .Where(message =>
                message.Text.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                message.SenderName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                message.MatrixRoomId.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Select(message => new SearchResultViewModel(message.SenderName, message.Text, "Message", message.MatrixRoomId, message.SentAt))
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
            .Select(result =>
            {
                if (roomNames.TryGetValue(result.SourceRoom, out var roomName))
                {
                    return result with
                    {
                        Title = string.Equals(result.Kind, "Message", StringComparison.Ordinal)
                            ? MessagePresentationFormatter.ResolveDisplaySenderName(result.Title, roomName)
                            : result.Title,
                        SourceRoom = roomName
                    };
                }

                return LooksLikeMatrixRoomId(result.SourceRoom)
                    ? result with { SourceRoom = string.Empty }
                    : result;
            })
            .ToList();
    }

    private static bool LooksLikeMatrixRoomId(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.StartsWith("!", StringComparison.Ordinal) &&
               value.Contains(':', StringComparison.Ordinal);
    }
}
