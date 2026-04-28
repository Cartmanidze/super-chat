using SuperChat.Contracts.Features.Search;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Domain.Features.Messaging;
using SuperChat.Infrastructure.Features.Messaging;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Infrastructure.Features.Search;

internal static class SearchResultViewModelMappings
{
    public static SearchResultViewModel ToSearchResultViewModel(this WorkItemRecord item)
    {
        return new SearchResultViewModel(
            item.Title,
            item.Summary,
            item.Kind.ToString(),
            item.ExternalChatId,
            item.ObservedAt,
            item.ToResolutionNote(),
            item.ResolutionTrace?.Confidence);
    }

    public static SearchResultViewModel ToSearchResultViewModel(this ChatMessage message)
    {
        return new SearchResultViewModel(
            message.SenderName,
            message.Text,
            "Message",
            message.ExternalChatId,
            message.SentAt,
            null,
            null);
    }

    public static SearchResultViewModel WithResolvedChatTitle(
        this SearchResultViewModel result,
        IReadOnlyDictionary<string, string> chatTitles)
    {
        if (chatTitles.TryGetValue(result.ChatTitle, out var resolvedTitle))
        {
            return result with
            {
                Title = string.Equals(result.Kind, "Message", StringComparison.Ordinal)
                    ? MessagePresentationFormatter.ResolveDisplaySenderName(result.Title, resolvedTitle)
                    : result.Title,
                ChatTitle = resolvedTitle
            };
        }

        // Если читаемого имени нет, а текущее значение это сырой идентификатор чата
        // (числовой Telegram chat id или legacy Matrix room id), не показываем его.
        return ChatTitleHeuristics.LooksLikeRawChatId(result.ChatTitle)
            ? result with { ChatTitle = string.Empty }
            : result;
    }
}
