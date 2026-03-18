using SuperChat.Contracts.ViewModels;
using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Services;

internal static class EventWorkItemCardViewModelMapper
{
    public static EventWorkItemCardViewModel Map(
        MeetingRecord meeting,
        WorkItemMetadata metadata)
    {
        return new EventWorkItemCardViewModel(
            meeting.Title,
            meeting.Summary,
            ExtractedItemKind.Meeting.ToString(),
            meeting.ObservedAt,
            meeting.ScheduledFor,
            meeting.SourceRoom,
            metadata.Status.ToEventStatus() ?? EventStatus.PendingConfirmation,
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
