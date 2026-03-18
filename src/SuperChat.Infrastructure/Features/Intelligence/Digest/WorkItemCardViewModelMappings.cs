using SuperChat.Contracts.ViewModels;
using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Services;

internal static class WorkItemCardViewModelMappings
{
    public static WorkItemCardViewModel ToWorkItemCardViewModel(this WorkItemRecord item, DateTimeOffset now)
    {
        var metadata = WorkItemPresentationMetadata.FromWorkItem(item, now);
        return metadata.Type switch
        {
            WorkItemType.Request => RequestWorkItemCardViewModelMapper.Map(item, metadata),
            _ => ActionItemWorkItemCardViewModelMapper.Map(item, metadata)
        };
    }

    public static WorkItemCardViewModel ToWorkItemCardViewModel(this MeetingRecord meeting, DateTimeOffset now)
    {
        var metadata = WorkItemPresentationMetadata.FromMeeting(meeting, now);
        return EventWorkItemCardViewModelMapper.Map(meeting, metadata);
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
