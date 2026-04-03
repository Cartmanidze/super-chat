using SuperChat.Contracts.Features.Chat;
using SuperChat.Contracts.Features.WorkItems;

namespace SuperChat.Infrastructure.Features.Chat;

internal static class MeetingChatResultItemViewModelMapper
{
    public static MeetingChatResultItemViewModel Map(ChatResultItemProjection projection)
    {
        var item = new MeetingChatResultItemViewModel(
            Title: projection.Title,
            Summary: projection.Summary,
            SourceRoom: projection.SourceRoom,
            Timestamp: projection.Timestamp,
            MeetingStatus: projection.Status.ToMeetingStatus() ?? MeetingStatus.PendingConfirmation,
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
