using SuperChat.Contracts.Features.Chat;
using SuperChat.Contracts.Features.WorkItems;

namespace SuperChat.Infrastructure.Features.Chat;

internal static class EventChatResultItemViewModelMapper
{
    public static EventChatResultItemViewModel Map(ChatResultItemProjection projection)
    {
        var item = new EventChatResultItemViewModel(
            Title: projection.Title,
            Summary: projection.Summary,
            SourceRoom: projection.SourceRoom,
            Timestamp: projection.Timestamp,
            EventStatus: projection.Status.ToEventStatus() ?? EventStatus.PendingConfirmation,
            PriorityValue: projection.Priority ?? WorkItemPriority.Normal,
            Owner: projection.Owner,
            OriginValue: projection.Origin ?? WorkItemOrigin.DetectedFromChat,
            ReviewStateValue: projection.ReviewState ?? AiReviewState.NeedsReview,
            PlannedAt: projection.PlannedAt,
            DueAt: projection.DueAt,
            Source: projection.Source,
            UpdatedAt: projection.UpdatedAt,
            IsOverdue: projection.IsOverdue,
            MeetingProvider: projection.MeetingProvider,
            MeetingJoinUrl: projection.MeetingJoinUrl);

        return item with { Id = projection.Id };
    }
}
