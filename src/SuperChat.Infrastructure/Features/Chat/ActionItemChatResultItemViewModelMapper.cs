using SuperChat.Contracts.Features.Chat;
using SuperChat.Contracts.Features.WorkItems;

namespace SuperChat.Infrastructure.Features.Chat;

internal static class ActionItemChatResultItemViewModelMapper
{
    public static ActionItemChatResultItemViewModel Map(ChatResultItemProjection projection)
    {
        var item = new ActionItemChatResultItemViewModel(
            Title: projection.Title,
            Summary: projection.Summary,
            SourceRoom: projection.SourceRoom,
            Timestamp: projection.Timestamp,
            ActionItemStatus: projection.Status.ToActionItemStatus() ?? ActionItemStatus.ToDo,
            PriorityValue: projection.Priority ?? WorkItemPriority.Normal,
            Owner: projection.Owner,
            OriginValue: projection.Origin ?? WorkItemOrigin.DetectedFromChat,
            ReviewStateValue: projection.ReviewState ?? AiReviewState.NeedsReview,
            PlannedAt: projection.PlannedAt,
            DueAt: projection.DueAt,
            Source: projection.Source,
            UpdatedAt: projection.UpdatedAt,
            IsOverdue: projection.IsOverdue);

        return item with { Id = projection.Id };
    }
}
