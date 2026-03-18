using SuperChat.Contracts.ViewModels;

namespace SuperChat.Infrastructure.Services;

internal static class RequestChatResultItemViewModelMapper
{
    public static RequestChatResultItemViewModel Map(ChatResultItemProjection projection)
    {
        var item = new RequestChatResultItemViewModel(
            Title: projection.Title,
            Summary: projection.Summary,
            SourceRoom: projection.SourceRoom,
            Timestamp: projection.Timestamp,
            RequestStatus: projection.Status.ToRequestStatus() ?? RequestStatus.AwaitingResponse,
            PriorityValue: projection.Priority ?? WorkItemPriority.Normal,
            Owner: projection.Owner,
            OriginValue: projection.Origin ?? WorkItemOrigin.Request,
            ReviewStateValue: projection.ReviewState ?? AiReviewState.NeedsReview,
            PlannedAt: projection.PlannedAt,
            DueAt: projection.DueAt,
            Source: projection.Source,
            UpdatedAt: projection.UpdatedAt,
            IsOverdue: projection.IsOverdue);

        return item with { ActionKey = projection.ActionKey };
    }
}
