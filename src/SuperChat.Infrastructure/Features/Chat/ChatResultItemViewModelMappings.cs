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
        var projection = ChatResultItemProjectionFactory.FromCard(card, genericTitle);

        return card switch
        {
            RequestWorkItemCardViewModel requestCard => RequestChatResultItemViewModelMapper.Map(
                projection with { Status = requestCard.RequestStatus.ToWorkItemStatus() }),
            EventWorkItemCardViewModel eventCard => EventChatResultItemViewModelMapper.Map(
                projection with { Status = eventCard.EventStatus.ToWorkItemStatus() }),
            ActionItemWorkItemCardViewModel actionItemCard => ActionItemChatResultItemViewModelMapper.Map(
                projection with { Status = actionItemCard.ActionItemStatus.ToWorkItemStatus() }),
            _ => MapProjection(projection)
        };
    }

    public static ChatResultItemViewModel ToChatResultItemViewModel(this SearchResultViewModel result)
    {
        return MapProjection(ChatResultItemProjectionFactory.FromSearchResult(result));
    }

    public static ChatResultItemViewModel ToChatResultItemViewModel(this NormalizedMessage message, string sourceRoom)
    {
        return GenericChatResultItemViewModelMapper.Map(
            ChatResultItemProjectionFactory.FromMessage(message, sourceRoom));
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
            EventChatResultItemViewModel eventItem => EventChatResultItemViewModelMapper.Map(
                projection with { Status = eventItem.EventStatus.ToWorkItemStatus() }),
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
            WorkItemType.Event => EventChatResultItemViewModelMapper.Map(NormalizeEventProjection(projection)),
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

    private static ChatResultItemProjection NormalizeEventProjection(ChatResultItemProjection projection)
    {
        return projection with
        {
            Type = WorkItemType.Event,
            Status = ResolveEventStatus(projection).ToWorkItemStatus(),
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

    private static EventStatus ResolveEventStatus(ChatResultItemProjection projection)
    {
        return (projection.Status ?? WorkItemPresentationMetadata.ResolveStatus(projection.Kind, projection.Summary)).ToEventStatus()
               ?? EventStatus.PendingConfirmation;
    }

    private static ActionItemStatus ResolveActionItemStatus(ChatResultItemProjection projection)
    {
        return (projection.Status ?? WorkItemPresentationMetadata.ResolveStatus(projection.Kind, projection.Summary)).ToActionItemStatus()
               ?? ActionItemStatus.ToDo;
    }
}
