using SuperChat.Contracts.ViewModels;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

internal static class ChatResultItemViewModelMappings
{
    public static ChatResultItemViewModel ToChatResultItemViewModel(
        this WorkItemCardViewModel card,
        string? genericTitle = null)
    {
        var type = card.Type ?? WorkItemPresentationMetadata.ResolveType(card.Kind);
        var timestamp = string.Equals(card.Kind, ExtractedItemKind.Meeting.ToString(), StringComparison.Ordinal)
            ? card.PlannedAt ?? card.DueAt ?? card.ObservedAt
            : card.ObservedAt;
        var title = !string.IsNullOrWhiteSpace(genericTitle) &&
                    string.Equals(card.Title, genericTitle, StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(card.Summary)
            ? card.Summary
            : card.Title;
        var summary = !string.IsNullOrWhiteSpace(genericTitle) &&
                      string.Equals(card.Title, genericTitle, StringComparison.Ordinal) &&
                      !string.IsNullOrWhiteSpace(card.Summary)
            ? string.Empty
            : card.Summary;

        if (card is RequestWorkItemCardViewModel requestCard)
        {
            return new RequestChatResultItemViewModel(
                Title: title,
                Summary: summary,
                SourceRoom: card.SourceRoom,
                Timestamp: timestamp,
                RequestStatus: requestCard.RequestStatus,
                PriorityValue: card.Priority,
                Owner: requestCard.Owner,
                OriginValue: requestCard.Origin,
                ReviewStateValue: card.ReviewState,
                PlannedAt: card.PlannedAt,
                DueAt: card.DueAt,
                Source: card.Source,
                UpdatedAt: card.UpdatedAt ?? card.ObservedAt,
                IsOverdue: card.IsOverdue);
        }

        if (card is EventWorkItemCardViewModel eventCard)
        {
            return new EventChatResultItemViewModel(
                Title: title,
                Summary: summary,
                SourceRoom: card.SourceRoom,
                Timestamp: timestamp,
                EventStatus: eventCard.EventStatus,
                PriorityValue: card.Priority,
                Owner: eventCard.Owner,
                OriginValue: eventCard.Origin,
                ReviewStateValue: card.ReviewState,
                PlannedAt: card.PlannedAt,
                DueAt: card.DueAt,
                Source: card.Source,
                UpdatedAt: card.UpdatedAt ?? card.ObservedAt,
                IsOverdue: card.IsOverdue,
                MeetingProvider: card.MeetingProvider,
                MeetingJoinUrl: card.MeetingJoinUrl);
        }

        if (card is ObligationWorkItemCardViewModel obligationCard)
        {
            return new ObligationChatResultItemViewModel(
                Title: title,
                Summary: summary,
                SourceRoom: card.SourceRoom,
                Timestamp: timestamp,
                ObligationStatus: obligationCard.ObligationStatus,
                PriorityValue: card.Priority,
                Owner: obligationCard.Owner,
                OriginValue: obligationCard.Origin,
                ReviewStateValue: card.ReviewState,
                PlannedAt: card.PlannedAt,
                DueAt: card.DueAt,
                Source: card.Source,
                UpdatedAt: card.UpdatedAt ?? card.ObservedAt,
                IsOverdue: card.IsOverdue);
        }

        return type switch
        {
            WorkItemType.Request => new RequestChatResultItemViewModel(
                Title: title,
                Summary: summary,
                SourceRoom: card.SourceRoom,
                Timestamp: timestamp,
                RequestStatus: (card.Status ?? WorkItemPresentationMetadata.ResolveStatus(card.Kind, card.Summary)).ToRequestStatus() ?? RequestStatus.AwaitingResponse,
                PriorityValue: card.Priority,
                Owner: card.Owner ?? WorkItemPresentationMetadata.ResolveOwner(card.Kind),
                OriginValue: WorkItemPresentationMetadata.ResolveOrigin(card.Kind) ?? card.Origin,
                ReviewStateValue: card.ReviewState,
                PlannedAt: card.PlannedAt,
                DueAt: card.DueAt,
                Source: card.Source,
                UpdatedAt: card.UpdatedAt ?? card.ObservedAt,
                IsOverdue: card.IsOverdue),
            WorkItemType.Event => new EventChatResultItemViewModel(
                Title: title,
                Summary: summary,
                SourceRoom: card.SourceRoom,
                Timestamp: timestamp,
                EventStatus: (card.Status ?? WorkItemPresentationMetadata.ResolveStatus(card.Kind, card.Summary)).ToEventStatus() ?? EventStatus.PendingConfirmation,
                PriorityValue: card.Priority,
                Owner: card.Owner ?? WorkItemPresentationMetadata.ResolveOwner(card.Kind),
                OriginValue: WorkItemPresentationMetadata.ResolveOrigin(card.Kind) ?? card.Origin,
                ReviewStateValue: card.ReviewState,
                PlannedAt: card.PlannedAt,
                DueAt: card.DueAt,
                Source: card.Source,
                UpdatedAt: card.UpdatedAt ?? card.ObservedAt,
                IsOverdue: card.IsOverdue,
                MeetingProvider: card.MeetingProvider,
                MeetingJoinUrl: card.MeetingJoinUrl),
            WorkItemType.Obligation => new ObligationChatResultItemViewModel(
                Title: title,
                Summary: summary,
                SourceRoom: card.SourceRoom,
                Timestamp: timestamp,
                ObligationStatus: (card.Status ?? WorkItemPresentationMetadata.ResolveStatus(card.Kind, card.Summary)).ToObligationStatus() ?? ObligationStatus.ToDo,
                PriorityValue: card.Priority,
                Owner: card.Owner ?? WorkItemPresentationMetadata.ResolveOwner(card.Kind),
                OriginValue: WorkItemPresentationMetadata.ResolveOrigin(card.Kind) ?? card.Origin,
                ReviewStateValue: card.ReviewState,
                PlannedAt: card.PlannedAt,
                DueAt: card.DueAt,
                Source: card.Source,
                UpdatedAt: card.UpdatedAt ?? card.ObservedAt,
                IsOverdue: card.IsOverdue),
            _ => new GenericChatResultItemViewModel(
                title,
                summary,
                card.SourceRoom,
                timestamp,
                card.Type,
                card.Status,
                card.Priority,
                card.Owner,
                card.Origin,
                card.ReviewState,
                card.PlannedAt,
                card.DueAt,
                card.Source,
                card.UpdatedAt ?? card.ObservedAt,
                card.IsOverdue,
                card.MeetingProvider,
                card.MeetingJoinUrl)
        };
    }

    public static ChatResultItemViewModel ToChatResultItemViewModel(this SearchResultViewModel result)
    {
        var type = WorkItemPresentationMetadata.ResolveType(result.Kind);
        var status = WorkItemPresentationMetadata.ResolveStatus(result.Kind, result.Summary);
        var owner = WorkItemPresentationMetadata.ResolveOwner(result.Kind);
        var origin = WorkItemPresentationMetadata.ResolveOrigin(result.Kind);
        var joinLink = type == WorkItemType.Event
            ? MeetingJoinLinkParser.TryParse(result.Summary)
            : null;
        return type switch
        {
            WorkItemType.Request => new RequestChatResultItemViewModel(
                Title: result.Title,
                Summary: result.Summary,
                SourceRoom: result.SourceRoom,
                Timestamp: result.ObservedAt,
                RequestStatus: status.ToRequestStatus() ?? RequestStatus.AwaitingResponse,
                PriorityValue: WorkItemPriority.Normal,
                Owner: owner,
                OriginValue: origin ?? WorkItemOrigin.Request,
                ReviewStateValue: AiReviewState.NeedsReview,
                Source: WorkItemSource.Chat,
                UpdatedAt: result.ObservedAt),
            WorkItemType.Event => new EventChatResultItemViewModel(
                Title: result.Title,
                Summary: result.Summary,
                SourceRoom: result.SourceRoom,
                Timestamp: result.ObservedAt,
                EventStatus: status.ToEventStatus() ?? EventStatus.PendingConfirmation,
                PriorityValue: WorkItemPriority.Normal,
                Owner: owner,
                OriginValue: origin ?? WorkItemOrigin.DetectedFromChat,
                ReviewStateValue: AiReviewState.NeedsReview,
                Source: WorkItemSource.Chat,
                UpdatedAt: result.ObservedAt,
                MeetingProvider: joinLink?.Provider,
                MeetingJoinUrl: joinLink?.Url),
            WorkItemType.Obligation => new ObligationChatResultItemViewModel(
                Title: result.Title,
                Summary: result.Summary,
                SourceRoom: result.SourceRoom,
                Timestamp: result.ObservedAt,
                ObligationStatus: status.ToObligationStatus() ?? ObligationStatus.ToDo,
                PriorityValue: WorkItemPriority.Normal,
                Owner: owner,
                OriginValue: origin ?? WorkItemOrigin.DetectedFromChat,
                ReviewStateValue: AiReviewState.NeedsReview,
                Source: WorkItemSource.Chat,
                UpdatedAt: result.ObservedAt),
            _ => new GenericChatResultItemViewModel(
                result.Title,
                result.Summary,
                result.SourceRoom,
                result.ObservedAt,
                type,
                status,
                type is null ? null : WorkItemPriority.Normal,
                owner,
                origin,
                type is null ? null : AiReviewState.NeedsReview,
                null,
                null,
                WorkItemSource.Chat,
                result.ObservedAt,
                false,
                joinLink?.Provider,
                joinLink?.Url)
        };
    }

    public static ChatResultItemViewModel ToChatResultItemViewModel(this NormalizedMessage message, string sourceRoom)
    {
        var joinLink = MeetingJoinLinkParser.TryParse(message.Text);
        return new GenericChatResultItemViewModel(
            MessagePresentationFormatter.ResolveDisplaySenderName(message.SenderName, sourceRoom),
            message.Text,
            sourceRoom,
            message.SentAt,
            Source: WorkItemSource.Chat,
            UpdatedAt: message.SentAt,
            MeetingProvider: joinLink?.Provider,
            MeetingJoinUrl: joinLink?.Url);
    }

    public static ChatResultItemViewModel ToChatResultItemViewModel(this RetrievedChatContext context)
    {
        var joinLink = MeetingJoinLinkParser.TryParse(context.Text);
        return new GenericChatResultItemViewModel(
            context.Title,
            context.Summary,
            context.SourceRoom,
            context.ObservedAt,
            Source: WorkItemSource.Chat,
            UpdatedAt: context.ObservedAt,
            MeetingProvider: joinLink?.Provider,
            MeetingJoinUrl: joinLink?.Url);
    }

    public static ChatResultItemViewModel ToChatResultItemViewModel(
        this GeneratedChatAnswerItem item,
        RetrievedChatContext context)
    {
        var joinLink = MeetingJoinLinkParser.TryParse(context.Text);
        return new GenericChatResultItemViewModel(
            item.Title,
            item.Summary,
            context.SourceRoom,
            context.ObservedAt,
            Source: WorkItemSource.Chat,
            UpdatedAt: context.ObservedAt,
            MeetingProvider: joinLink?.Provider,
            MeetingJoinUrl: joinLink?.Url);
    }

    public static ChatResultItemViewModel ToChatResultItemViewModel(
        this GeneratedChatAnswerItem item,
        ChatResultItemViewModel sourceItem)
    {
        return sourceItem switch
        {
            RequestChatResultItemViewModel requestItem => new RequestChatResultItemViewModel(
                Title: item.Title,
                Summary: item.Summary,
                SourceRoom: sourceItem.SourceRoom,
                Timestamp: sourceItem.Timestamp,
                RequestStatus: requestItem.RequestStatus,
                PriorityValue: sourceItem.Priority ?? WorkItemPriority.Normal,
                Owner: sourceItem.Owner,
                OriginValue: sourceItem.Origin ?? WorkItemOrigin.Request,
                ReviewStateValue: sourceItem.ReviewState ?? AiReviewState.NeedsReview,
                PlannedAt: sourceItem.PlannedAt,
                DueAt: sourceItem.DueAt,
                Source: sourceItem.Source,
                UpdatedAt: sourceItem.UpdatedAt,
                IsOverdue: sourceItem.IsOverdue),
            EventChatResultItemViewModel eventItem => new EventChatResultItemViewModel(
                Title: item.Title,
                Summary: item.Summary,
                SourceRoom: sourceItem.SourceRoom,
                Timestamp: sourceItem.Timestamp,
                EventStatus: eventItem.EventStatus,
                PriorityValue: sourceItem.Priority ?? WorkItemPriority.Normal,
                Owner: sourceItem.Owner,
                OriginValue: sourceItem.Origin ?? WorkItemOrigin.DetectedFromChat,
                ReviewStateValue: sourceItem.ReviewState ?? AiReviewState.NeedsReview,
                PlannedAt: sourceItem.PlannedAt,
                DueAt: sourceItem.DueAt,
                Source: sourceItem.Source,
                UpdatedAt: sourceItem.UpdatedAt,
                IsOverdue: sourceItem.IsOverdue,
                MeetingProvider: sourceItem.MeetingProvider,
                MeetingJoinUrl: sourceItem.MeetingJoinUrl),
            ObligationChatResultItemViewModel obligationItem => new ObligationChatResultItemViewModel(
                Title: item.Title,
                Summary: item.Summary,
                SourceRoom: sourceItem.SourceRoom,
                Timestamp: sourceItem.Timestamp,
                ObligationStatus: obligationItem.ObligationStatus,
                PriorityValue: sourceItem.Priority ?? WorkItemPriority.Normal,
                Owner: sourceItem.Owner,
                OriginValue: sourceItem.Origin ?? WorkItemOrigin.DetectedFromChat,
                ReviewStateValue: sourceItem.ReviewState ?? AiReviewState.NeedsReview,
                PlannedAt: sourceItem.PlannedAt,
                DueAt: sourceItem.DueAt,
                Source: sourceItem.Source,
                UpdatedAt: sourceItem.UpdatedAt,
                IsOverdue: sourceItem.IsOverdue),
            _ => new GenericChatResultItemViewModel(
                item.Title,
                item.Summary,
                sourceItem.SourceRoom,
                sourceItem.Timestamp,
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
                sourceItem.MeetingJoinUrl)
        };
    }
}
