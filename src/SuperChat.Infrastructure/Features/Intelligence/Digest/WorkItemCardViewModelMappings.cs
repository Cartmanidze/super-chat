using SuperChat.Contracts.ViewModels;
using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Services;

internal static class WorkItemCardViewModelMappings
{
    public static WorkItemCardViewModel ToWorkItemCardViewModel(this ExtractedItem item, DateTimeOffset now)
    {
        var metadata = WorkItemPresentationMetadata.FromExtractedItem(item, now);
        return metadata.Type switch
        {
            WorkItemType.Request => new RequestWorkItemCardViewModel(
                item.Title,
                item.Summary,
                item.Kind.ToString(),
                item.ObservedAt,
                item.DueAt,
                item.SourceRoom,
                metadata.Status.ToRequestStatus() ?? RequestStatus.AwaitingResponse,
                item.Confidence,
                metadata.Priority,
                metadata.Owner,
                metadata.Origin ?? WorkItemOrigin.Request,
                metadata.ReviewState,
                metadata.PlannedAt,
                metadata.Source,
                metadata.UpdatedAt,
                metadata.IsOverdue),
            WorkItemType.Event => new EventWorkItemCardViewModel(
                item.Title,
                item.Summary,
                item.Kind.ToString(),
                item.ObservedAt,
                item.DueAt,
                item.SourceRoom,
                metadata.Status.ToEventStatus() ?? EventStatus.PendingConfirmation,
                item.Confidence,
                metadata.Priority,
                metadata.Owner,
                metadata.Origin ?? WorkItemOrigin.DetectedFromChat,
                metadata.ReviewState,
                metadata.PlannedAt,
                metadata.Source,
                metadata.UpdatedAt,
                metadata.IsOverdue,
                metadata.MeetingProvider,
                metadata.MeetingJoinUrl),
            _ => new ObligationWorkItemCardViewModel(
                item.Title,
                item.Summary,
                item.Kind.ToString(),
                item.ObservedAt,
                item.DueAt,
                item.SourceRoom,
                metadata.Status.ToObligationStatus() ?? ObligationStatus.ToDo,
                item.Confidence,
                metadata.Priority,
                metadata.Owner,
                metadata.Origin ?? WorkItemOrigin.DetectedFromChat,
                metadata.ReviewState,
                metadata.PlannedAt,
                metadata.Source,
                metadata.UpdatedAt,
                metadata.IsOverdue)
        };
    }

    public static WorkItemCardViewModel ToWorkItemCardViewModel(this MeetingRecord meeting, DateTimeOffset now)
    {
        var metadata = WorkItemPresentationMetadata.FromMeeting(meeting, now);
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
            metadata.MeetingJoinUrl);
    }

    public static WorkItemCardViewModel WithResolvedSourceRoom(
        this WorkItemCardViewModel card,
        IReadOnlyDictionary<string, string> roomNames)
    {
        if (roomNames.TryGetValue(card.SourceRoom, out var roomName))
        {
            return card with { SourceRoom = roomName };
        }

        return card.SourceRoom.LooksLikeMatrixRoomId()
            ? card with { SourceRoom = string.Empty }
            : card;
    }

    private static bool LooksLikeMatrixRoomId(this string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.StartsWith("!", StringComparison.Ordinal) &&
               value.Contains(':', StringComparison.Ordinal);
    }
}
