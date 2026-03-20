using SuperChat.Contracts.Features.Search;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Domain.Features.Messaging;
using SuperChat.Infrastructure.Features.Messaging;

namespace SuperChat.Infrastructure.Features.Search;

internal static class SearchResultViewModelMappings
{
    public static SearchResultViewModel ToSearchResultViewModel(this WorkItemRecord item)
    {
        return new SearchResultViewModel(
            item.Title,
            item.Summary,
            item.Kind.ToString(),
            item.SourceRoom,
            item.ObservedAt);
    }

    public static SearchResultViewModel ToSearchResultViewModel(this NormalizedMessage message)
    {
        return new SearchResultViewModel(
            message.SenderName,
            message.Text,
            "Message",
            message.MatrixRoomId,
            message.SentAt);
    }

    public static SearchResultViewModel WithResolvedSourceRoom(
        this SearchResultViewModel result,
        IReadOnlyDictionary<string, string> roomNames)
    {
        if (roomNames.TryGetValue(result.SourceRoom, out var roomName))
        {
            return result with
            {
                Title = string.Equals(result.Kind, "Message", StringComparison.Ordinal)
                    ? MessagePresentationFormatter.ResolveDisplaySenderName(result.Title, roomName)
                    : result.Title,
                SourceRoom = roomName
            };
        }

        return result.SourceRoom.LooksLikeMatrixRoomId()
            ? result with { SourceRoom = string.Empty }
            : result;
    }

    private static bool LooksLikeMatrixRoomId(this string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.StartsWith("!", StringComparison.Ordinal) &&
               value.Contains(':', StringComparison.Ordinal);
    }
}
