using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

internal static class WorkItemResolutionState
{
    public const string Completed = "completed";
    public const string Dismissed = "dismissed";
    public const string Manual = "manual";
    public const string AutoReply = "auto_reply";
    public const string AutoCompletion = "auto_completion";
    public const string AutoMeetingCompletion = "auto_meeting_completion";

    public static bool IsResolved(this ExtractedItemEntity item)
    {
        return item.ResolvedAt is not null;
    }

    public static bool IsResolved(this MeetingEntity item)
    {
        return item.ResolvedAt is not null;
    }
}
