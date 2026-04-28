using SuperChat.Contracts.Features.Chat;
using SuperChat.Contracts.Features.Search;
using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Messaging;
using SuperChat.Infrastructure.Features.Messaging;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Infrastructure.Features.Chat;

internal static class ChatResultItemProjectionFactory
{
    public static ChatResultItemProjection FromCard(
        WorkItemCardViewModel card,
        string? genericTitle = null)
    {
        var timestamp = card.Type == WorkItemType.Meeting
            ? card.PlannedAt ?? card.DueAt ?? card.ObservedAt
            : card.ObservedAt;
        var titleMatchesGenericTitle = !string.IsNullOrWhiteSpace(genericTitle) &&
                                       string.Equals(card.Title, genericTitle, StringComparison.Ordinal) &&
                                       !string.IsNullOrWhiteSpace(card.Summary);

        return new ChatResultItemProjection(
            card.Id,
            titleMatchesGenericTitle ? card.Summary : card.Title,
            titleMatchesGenericTitle ? string.Empty : card.Summary,
            card.ChatTitle,
            timestamp,
            Type: card.Type,
            Status: card.Status,
            Priority: card.Priority,
            Owner: card.Owner,
            Origin: card.Origin,
            ReviewState: card.ReviewState,
            PlannedAt: card.PlannedAt,
            DueAt: card.DueAt,
            Source: card.Source,
            UpdatedAt: card.UpdatedAt ?? card.ObservedAt,
            IsOverdue: card.IsOverdue,
            MeetingProvider: card.MeetingProvider,
            MeetingJoinUrl: card.MeetingJoinUrl);
    }

    public static ChatResultItemProjection FromSearchResult(SearchResultViewModel result)
    {
        var type = WorkItemPresentationMetadata.ResolveType(result.Kind);
        var joinLink = type == WorkItemType.Meeting
            ? MeetingJoinLinkParser.TryParse(result.Summary)
            : null;

        return new ChatResultItemProjection(
            null,
            result.Title,
            result.Summary,
            result.ChatTitle,
            result.ObservedAt,
            result.Kind,
            type,
            WorkItemPresentationMetadata.ResolveStatus(result.Kind, result.Summary),
            type is null ? null : WorkItemPriority.Normal,
            WorkItemPresentationMetadata.ResolveOwner(result.Kind),
            WorkItemPresentationMetadata.ResolveOrigin(result.Kind),
            type is null ? null : AiReviewState.NeedsReview,
            null,
            null,
            WorkItemSource.Chat,
            result.ObservedAt,
            false,
            joinLink?.Provider,
            joinLink?.Url);
    }

    public static ChatResultItemProjection FromMessage(ChatMessage message, string chatTitle)
    {
        var joinLink = MeetingJoinLinkParser.TryParse(message.Text);
        return new ChatResultItemProjection(
            null,
            MessagePresentationFormatter.ResolveDisplaySenderName(message.SenderName, chatTitle),
            message.Text,
            chatTitle,
            message.SentAt,
            Source: WorkItemSource.Chat,
            UpdatedAt: message.SentAt,
            MeetingProvider: joinLink?.Provider,
            MeetingJoinUrl: joinLink?.Url);
    }

    public static ChatResultItemProjection FromContext(RetrievedChatContext context)
    {
        var joinLink = MeetingJoinLinkParser.TryParse(context.Text);
        return new ChatResultItemProjection(
            null,
            context.Title,
            context.Summary,
            context.ExternalChatId,
            context.ObservedAt,
            Source: WorkItemSource.Chat,
            UpdatedAt: context.ObservedAt,
            MeetingProvider: joinLink?.Provider,
            MeetingJoinUrl: joinLink?.Url);
    }

    public static ChatResultItemProjection FromGeneratedItem(
        GeneratedChatAnswerItem item,
        RetrievedChatContext context)
    {
        var joinLink = MeetingJoinLinkParser.TryParse(context.Text);
        return new ChatResultItemProjection(
            null,
            item.Title,
            item.Summary,
            context.ExternalChatId,
            context.ObservedAt,
            Source: WorkItemSource.Chat,
            UpdatedAt: context.ObservedAt,
            MeetingProvider: joinLink?.Provider,
            MeetingJoinUrl: joinLink?.Url);
    }

    public static ChatResultItemProjection FromGeneratedItem(
        GeneratedChatAnswerItem item,
        ChatResultItemViewModel sourceItem)
    {
        return new ChatResultItemProjection(
            sourceItem.Id,
            item.Title,
            item.Summary,
            sourceItem.ChatTitle,
            sourceItem.Timestamp,
            null,
            sourceItem.Type,
            sourceItem.Status,
            sourceItem.Priority,
            sourceItem.Owner,
            sourceItem.Origin,
            sourceItem.ReviewState,
            sourceItem.PlannedAt,
            sourceItem.DueAt,
            sourceItem.Source,
            sourceItem.UpdatedAt,
            sourceItem.IsOverdue,
            sourceItem.MeetingProvider,
            sourceItem.MeetingJoinUrl);
    }
}
