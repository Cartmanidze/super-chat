using SuperChat.Contracts.ViewModels;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.State;

namespace SuperChat.Infrastructure.Services;

public sealed class SearchService(SuperChatStore store) : ISearchService
{
    public Task<IReadOnlyList<SearchResultViewModel>> SearchAsync(Guid userId, string query, CancellationToken cancellationToken)
    {
        var normalizedQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return Task.FromResult<IReadOnlyList<SearchResultViewModel>>([]);
        }

        var results = store.GetExtractedItems(userId)
            .Where(item =>
                item.Title.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                item.Summary.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                item.SourceRoom.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.ObservedAt)
            .Take(20)
            .Select(item => new SearchResultViewModel(item.Title, item.Summary, item.Kind.ToString(), item.SourceRoom, item.ObservedAt))
            .ToList();

        if (results.Count == 0)
        {
            results = store.GetRecentMessages(userId, 20)
                .Where(message =>
                    message.Text.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                    message.SenderName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                    message.MatrixRoomId.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                .Select(message => new SearchResultViewModel(message.SenderName, message.Text, "Message", message.MatrixRoomId, message.SentAt))
                .ToList();
        }

        return Task.FromResult<IReadOnlyList<SearchResultViewModel>>(results);
    }
}
