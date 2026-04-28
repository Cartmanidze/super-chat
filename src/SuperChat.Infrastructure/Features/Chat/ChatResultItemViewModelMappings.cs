using SuperChat.Contracts.Features.Chat;
using SuperChat.Contracts.Features.Search;
using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Messaging;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Infrastructure.Features.Chat;

internal static class ChatResultItemViewModelMappings
{
    public static ChatResultItemViewModel ToChatResultItemViewModel(
        this WorkItemCardViewModel card,
        string? genericTitle = null)
    {
        var projection = ChatResultItemProjectionFactory.FromCard(card, genericTitle);

        return card switch
        {
            MeetingWorkItemCardViewModel meetingCard => MeetingChatResultItemViewModelMapper.Map(
                projection with { Status = meetingCard.MeetingStatus.ToWorkItemStatus() }),
            _ => MapProjection(projection)
        };
    }

    public static ChatResultItemViewModel ToChatResultItemViewModel(this SearchResultViewModel result)
    {
        return MapProjection(ChatResultItemProjectionFactory.FromSearchResult(result));
    }

    public static ChatResultItemViewModel ToChatResultItemViewModel(this ChatMessage message, string chatTitle)
    {
        return GenericChatResultItemViewModelMapper.Map(
            ChatResultItemProjectionFactory.FromMessage(message, chatTitle));
    }

    public static ChatResultItemViewModel ToChatResultItemViewModel(this RetrievedChatContext context)
    {
        return GenericChatResultItemViewModelMapper.Map(
            ChatResultItemProjectionFactory.FromContext(context));
    }

    public static ChatResultItemViewModel ToChatResultItemViewModel(
        this GeneratedChatAnswerItem item,
        RetrievedChatContext context)
    {
        return GenericChatResultItemViewModelMapper.Map(
            ChatResultItemProjectionFactory.FromGeneratedItem(item, context));
    }

    public static ChatResultItemViewModel ToChatResultItemViewModel(
        this GeneratedChatAnswerItem item,
        ChatResultItemViewModel sourceItem)
    {
        var projection = ChatResultItemProjectionFactory.FromGeneratedItem(item, sourceItem);

        return sourceItem switch
        {
            RequestChatResultItemViewModel requestItem => RequestChatResultItemViewModelMapper.Map(
                projection with { Status = requestItem.RequestStatus.ToWorkItemStatus() }),
            MeetingChatResultItemViewModel meetingItem => MeetingChatResultItemViewModelMapper.Map(
                projection with { Status = meetingItem.MeetingStatus.ToWorkItemStatus() }),
            ActionItemChatResultItemViewModel actionItemItem => ActionItemChatResultItemViewModelMapper.Map(
                projection with { Status = actionItemItem.ActionItemStatus.ToWorkItemStatus() }),
            _ => GenericChatResultItemViewModelMapper.Map(projection)
        };
    }

    private static ChatResultItemViewModel MapProjection(ChatResultItemProjection projection)
    {
        var type = projection.Type ?? WorkItemPresentationMetadata.ResolveType(projection.Kind);
        return type switch
        {
            WorkItemType.Request => RequestChatResultItemViewModelMapper.Map(NormalizeRequestProjection(projection)),
            WorkItemType.Meeting => MeetingChatResultItemViewModelMapper.Map(NormalizeMeetingProjection(projection)),
            WorkItemType.ActionItem => ActionItemChatResultItemViewModelMapper.Map(NormalizeActionItemProjection(projection)),
            _ => GenericChatResultItemViewModelMapper.Map(projection)
        };
    }

    private static ChatResultItemProjection NormalizeRequestProjection(ChatResultItemProjection projection)
    {
        return projection with
        {
            Type = WorkItemType.Request,
            Status = ResolveRequestStatus(projection).ToWorkItemStatus(),
            Priority = projection.Priority ?? WorkItemPriority.Normal,
            Owner = projection.Owner ?? WorkItemPresentationMetadata.ResolveOwner(projection.Kind),
            Origin = projection.Origin ?? WorkItemPresentationMetadata.ResolveOrigin(projection.Kind) ?? WorkItemOrigin.Request,
            ReviewState = projection.ReviewState ?? AiReviewState.NeedsReview
        };
    }

    private static ChatResultItemProjection NormalizeMeetingProjection(ChatResultItemProjection projection)
    {
        return projection with
        {
            Type = WorkItemType.Meeting,
            Status = ResolveMeetingStatus(projection).ToWorkItemStatus(),
            Priority = projection.Priority ?? WorkItemPriority.Normal,
            Owner = projection.Owner ?? WorkItemPresentationMetadata.ResolveOwner(projection.Kind),
            Origin = projection.Origin ?? WorkItemPresentationMetadata.ResolveOrigin(projection.Kind) ?? WorkItemOrigin.DetectedFromChat,
            ReviewState = projection.ReviewState ?? AiReviewState.NeedsReview
        };
    }

    private static ChatResultItemProjection NormalizeActionItemProjection(ChatResultItemProjection projection)
    {
        return projection with
        {
            Type = WorkItemType.ActionItem,
            Status = ResolveActionItemStatus(projection).ToWorkItemStatus(),
            Priority = projection.Priority ?? WorkItemPriority.Normal,
            Owner = projection.Owner ?? WorkItemPresentationMetadata.ResolveOwner(projection.Kind),
            Origin = projection.Origin ?? WorkItemPresentationMetadata.ResolveOrigin(projection.Kind) ?? WorkItemOrigin.DetectedFromChat,
            ReviewState = projection.ReviewState ?? AiReviewState.NeedsReview
        };
    }

    private static RequestStatus ResolveRequestStatus(ChatResultItemProjection projection)
    {
        return (projection.Status ?? WorkItemPresentationMetadata.ResolveStatus(projection.Kind, projection.Summary)).ToRequestStatus()
               ?? RequestStatus.AwaitingResponse;
    }

    private static MeetingStatus ResolveMeetingStatus(ChatResultItemProjection projection)
    {
        return (projection.Status ?? WorkItemPresentationMetadata.ResolveStatus(projection.Kind, projection.Summary)).ToMeetingStatus()
               ?? MeetingStatus.PendingConfirmation;
    }

    private static ActionItemStatus ResolveActionItemStatus(ChatResultItemProjection projection)
    {
        return (projection.Status ?? WorkItemPresentationMetadata.ResolveStatus(projection.Kind, projection.Summary)).ToActionItemStatus()
               ?? ActionItemStatus.ToDo;
    }
}
