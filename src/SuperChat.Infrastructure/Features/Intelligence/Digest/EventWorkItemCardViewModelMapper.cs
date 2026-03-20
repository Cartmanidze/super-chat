using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Infrastructure.Features.Intelligence.Digest;

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
