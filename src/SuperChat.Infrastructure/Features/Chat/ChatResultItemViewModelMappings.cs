using SuperChat.Contracts.ViewModels;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

internal static class ChatResultItemViewModelMappings
{
    public static ChatResultItemViewModel ToChatResultItemViewModel(
        this DashboardCardViewModel card,
        string? genericTitle = null)
    {
        var timestamp = string.Equals(card.Kind, ExtractedItemKind.Meeting.ToString(), StringComparison.Ordinal)
            ? card.DueAt ?? card.ObservedAt
            : card.ObservedAt;

        if (!string.IsNullOrWhiteSpace(genericTitle) &&
            string.Equals(card.Title, genericTitle, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(card.Summary))
        {
            return new ChatResultItemViewModel(card.Summary, string.Empty, card.SourceRoom, timestamp);
        }

        return new ChatResultItemViewModel(card.Title, card.Summary, card.SourceRoom, timestamp);
    }

    public static ChatResultItemViewModel ToChatResultItemViewModel(this SearchResultViewModel result)
    {
        return new ChatResultItemViewModel(
            result.Title,
            result.Summary,
            result.SourceRoom,
            result.ObservedAt);
    }

    public static ChatResultItemViewModel ToChatResultItemViewModel(this NormalizedMessage message, string sourceRoom)
    {
        return new ChatResultItemViewModel(
            MessagePresentationFormatter.ResolveDisplaySenderName(message.SenderName, sourceRoom),
            message.Text,
            sourceRoom,
            message.SentAt);
    }

    public static ChatResultItemViewModel ToChatResultItemViewModel(this RetrievedChatContext context)
    {
        return new ChatResultItemViewModel(
            context.Title,
            context.Summary,
            context.SourceRoom,
            context.ObservedAt);
    }

    public static ChatResultItemViewModel ToChatResultItemViewModel(
        this GeneratedChatAnswerItem item,
        RetrievedChatContext context)
    {
        return new ChatResultItemViewModel(
            item.Title,
            item.Summary,
            context.SourceRoom,
            context.ObservedAt);
    }

    public static ChatResultItemViewModel ToChatResultItemViewModel(
        this GeneratedChatAnswerItem item,
        ChatResultItemViewModel sourceItem)
    {
        return new ChatResultItemViewModel(
            item.Title,
            item.Summary,
            sourceItem.SourceRoom,
            sourceItem.Timestamp);
    }
}
