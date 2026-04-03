using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Infrastructure.Features.Intelligence.Digest;

internal static class WorkItemCardViewModelMappings
{
    public static WorkItemCardViewModel ToWorkItemCardViewModel(this MeetingRecord meeting, DateTimeOffset now)
    {
        var metadata = WorkItemPresentationMetadata.FromMeeting(meeting, now);
        return MeetingWorkItemCardViewModelMapper.Map(meeting, metadata);
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
