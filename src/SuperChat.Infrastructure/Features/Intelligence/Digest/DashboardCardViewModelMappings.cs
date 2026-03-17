using SuperChat.Contracts.ViewModels;
using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Services;

internal static class DashboardCardViewModelMappings
{
    public static DashboardCardViewModel ToDashboardCardViewModel(this ExtractedItem item)
    {
        return new DashboardCardViewModel(
            item.Title,
            item.Summary,
            item.Kind.ToString(),
            item.ObservedAt,
            item.DueAt,
            item.SourceRoom,
            item.Confidence);
    }

    public static DashboardCardViewModel ToDashboardCardViewModel(this MeetingRecord meeting)
    {
        return new DashboardCardViewModel(
            meeting.Title,
            meeting.Summary,
            ExtractedItemKind.Meeting.ToString(),
            meeting.ObservedAt,
            meeting.ScheduledFor,
            meeting.SourceRoom,
            meeting.Confidence);
    }

    public static DashboardCardViewModel WithResolvedSourceRoom(
        this DashboardCardViewModel card,
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
