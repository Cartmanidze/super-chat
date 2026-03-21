using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Shared.Presentation;

internal static class WorkItemResolutionState
{
    public const string Completed = "completed";
    public const string Dismissed = "dismissed";
    public const string Missed = "missed";
    public const string Cancelled = "cancelled";
    public const string Rescheduled = "rescheduled";
    public const string Manual = "manual";
    public const string AutoReply = "auto_reply";
    public const string AutoCompletion = "auto_completion";
    public const string AutoMeetingCompletion = "auto_meeting_completion";
    public const string AutoAiReply = "auto_ai_reply";
    public const string AutoAiCompletion = "auto_ai_completion";
    public const string AutoAiMeetingCompletion = "auto_ai_meeting_completion";

    public static bool IsResolved(this ExtractedItemEntity item)
    {
        return item.ResolvedAt is not null;
    }

    public static bool IsResolved(this WorkItemEntity item)
    {
        return item.ResolvedAt is not null;
    }

    public static bool IsResolved(this MeetingEntity item)
    {
        return item.ResolvedAt is not null;
    }
}
