using SuperChat.Contracts.Features.Chat;

namespace SuperChat.Infrastructure.Features.Chat;

internal static class GenericChatResultItemViewModelMapper
{
    public static GenericChatResultItemViewModel Map(ChatResultItemProjection projection)
    {
        var item = new GenericChatResultItemViewModel(
            projection.Title,
            projection.Summary,
            projection.SourceRoom,
            projection.Timestamp,
            projection.Type,
            projection.Status,
            projection.Priority,
            projection.Owner,
            projection.Origin,
            projection.ReviewState,
            projection.PlannedAt,
            projection.DueAt,
            projection.Source,
            projection.UpdatedAt,
            projection.IsOverdue,
            projection.MeetingProvider,
            projection.MeetingJoinUrl);

        return item with { Id = projection.Id };
    }
}
