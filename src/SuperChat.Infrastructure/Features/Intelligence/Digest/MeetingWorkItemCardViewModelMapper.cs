using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Presentation;
using MeetingCardStatus = SuperChat.Contracts.Features.WorkItems.MeetingStatus;

namespace SuperChat.Infrastructure.Features.Intelligence.Digest;

internal static class MeetingWorkItemCardViewModelMapper
{
    public static MeetingWorkItemCardViewModel Map(
        MeetingRecord meeting,
        WorkItemMetadata metadata)
    {
        return new MeetingWorkItemCardViewModel(
            meeting.Title,
            meeting.Summary,
            meeting.ObservedAt,
            meeting.ScheduledFor,
            meeting.ExternalChatId,
            metadata.Status.ToMeetingStatus() ?? MeetingCardStatus.PendingConfirmation,
            meeting.Confidence,
            metadata.Priority,
            metadata.Owner,
            metadata.Origin ?? WorkItemOrigin.DetectedFromChat,
            metadata.ReviewState,
            metadata.PlannedAt,
            metadata.Source,
            metadata.UpdatedAt,
            metadata.IsOverdue,
            metadata.MeetingProvider,
            metadata.MeetingJoinUrl)
        {
            Id = meeting.Id
        };
    }
}
