using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;
using DomainMeetingStatus = SuperChat.Domain.Features.Intelligence.MeetingStatus;

namespace SuperChat.Infrastructure.Shared.Presentation;

internal static class WorkItemPresentationMetadata
{
    private static readonly string[] ImportantKeywords =
    [
        "важно", "срочно", "urgent", "asap", "priority", "критично"
    ];

    private static readonly string[] MeetingConfirmationKeywords =
    [
        "подтверждаю", "подтверждаем", "подтверждено", "confirmed", "confirm", "итого", "финально", "final", "договорились"
    ];

    private static readonly string[] MeetingRescheduleKeywords =
    [
        "давай", "лучше", "не могу", "не смогу", "не получится", "перенес", "перенос", "instead", "resched"
    ];

    private static readonly string[] MeetingCancellationKeywords =
    [
        "отмена", "отменили", "отменяется", "cancelled", "canceled", "cancel"
    ];

    public static WorkItemMetadata FromExtractedItem(ExtractedItem item, DateTimeOffset now)
    {
        return BuildWorkItemMetadata(item.Kind, item.Title, item.Summary, item.ObservedAt, item.DueAt, item.Confidence, now);
    }

    public static WorkItemMetadata FromWorkItem(WorkItemRecord item, DateTimeOffset now)
    {
        return BuildWorkItemMetadata(item.Kind, item.Title, item.Summary, item.ObservedAt, item.DueAt, item.Confidence, now);
    }

    private static WorkItemMetadata BuildWorkItemMetadata(
        ExtractedItemKind kindValue,
        string title,
        string summary,
        DateTimeOffset observedAt,
        DateTimeOffset? dueAt,
        double confidence,
        DateTimeOffset now)
    {
        var joinLink = kindValue == ExtractedItemKind.Meeting
            ? MeetingJoinLinkParser.TryParse(summary)
            : null;
        var plannedAt = kindValue == ExtractedItemKind.Meeting
            ? dueAt
            : null;

        var status = ResolveStatus(kindValue, summary);
        return new WorkItemMetadata(
            Type: ResolveType(kindValue),
            Status: status,
            Priority: ResolvePriority(title, summary, dueAt, now),
            Owner: ResolveOwner(kindValue),
            Origin: ResolveOrigin(kindValue),
            ReviewState: ResolveReviewState(confidence),
            PlannedAt: plannedAt,
            DueAt: dueAt,
            Source: WorkItemSource.Telegram,
            UpdatedAt: observedAt,
            IsOverdue: ResolveIsOverdue(status, dueAt, plannedAt, now),
            MeetingProvider: joinLink?.Provider,
            MeetingJoinUrl: joinLink?.Url);
    }

    public static WorkItemMetadata FromMeeting(MeetingRecord meeting, DateTimeOffset now)
    {
        var status = ToWorkItemStatus(meeting.Status);
        return new WorkItemMetadata(
            Type: WorkItemType.Meeting,
            Status: status,
            Priority: ResolvePriority(meeting.Title, meeting.Summary, meeting.ScheduledFor, now),
            Owner: WorkItemOwner.Both,
            Origin: WorkItemOrigin.DetectedFromChat,
            ReviewState: ResolveReviewState(meeting.Confidence),
            PlannedAt: meeting.ScheduledFor,
            DueAt: meeting.ScheduledFor,
            Source: WorkItemSource.Telegram,
            UpdatedAt: meeting.ObservedAt,
            IsOverdue: ResolveIsOverdue(status, meeting.ScheduledFor, meeting.ScheduledFor, now),
            MeetingProvider: MeetingJoinLinkParser.TryParseProvider(meeting.MeetingProvider),
            MeetingJoinUrl: meeting.MeetingJoinUrl);
    }

    public static WorkItemType? ResolveType(ExtractedItemKind kind)
    {
        return kind switch
        {
            ExtractedItemKind.WaitingOn => WorkItemType.Request,
            ExtractedItemKind.Meeting => WorkItemType.Meeting,
            ExtractedItemKind.Task => WorkItemType.ActionItem,
            ExtractedItemKind.Commitment => WorkItemType.ActionItem,
            _ => null
        };
    }

    public static WorkItemType? ResolveType(string? legacyKind)
    {
        return TryParseKind(legacyKind, out var kind)
            ? ResolveType(kind)
            : null;
    }

    public static WorkItemStatus? ResolveStatus(ExtractedItemKind kind, string summary)
    {
        return kind switch
        {
            ExtractedItemKind.WaitingOn => WorkItemStatus.AwaitingResponse,
            ExtractedItemKind.Task => WorkItemStatus.ToDo,
            ExtractedItemKind.Commitment => WorkItemStatus.ToDo,
            ExtractedItemKind.Meeting => ToWorkItemStatus(ResolveMeetingStatus(summary)),
            _ => null
        };
    }

    public static DomainMeetingStatus ResolveMeetingStatus(string summary)
    {
        var loweredSummary = summary.Trim().ToLowerInvariant();

        return loweredSummary switch
        {
            _ when ContainsAny(loweredSummary, MeetingCancellationKeywords) => DomainMeetingStatus.Cancelled,
            _ when ContainsAny(loweredSummary, MeetingRescheduleKeywords) => DomainMeetingStatus.Rescheduled,
            _ when ContainsAny(loweredSummary, MeetingConfirmationKeywords) => DomainMeetingStatus.Confirmed,
            _ => DomainMeetingStatus.PendingConfirmation
        };
    }

    public static WorkItemStatus ToWorkItemStatus(DomainMeetingStatus status)
    {
        return status switch
        {
            DomainMeetingStatus.Confirmed => WorkItemStatus.Confirmed,
            DomainMeetingStatus.Cancelled => WorkItemStatus.Cancelled,
            DomainMeetingStatus.Rescheduled => WorkItemStatus.Rescheduled,
            _ => WorkItemStatus.PendingConfirmation
        };
    }

    public static WorkItemStatus? ResolveStatus(string? legacyKind, string summary)
    {
        return TryParseKind(legacyKind, out var kind)
            ? ResolveStatus(kind, summary)
            : null;
    }

    public static WorkItemOwner? ResolveOwner(ExtractedItemKind kind)
    {
        return kind switch
        {
            ExtractedItemKind.WaitingOn => WorkItemOwner.Contact,
            ExtractedItemKind.Meeting => WorkItemOwner.Both,
            ExtractedItemKind.Task => WorkItemOwner.Me,
            ExtractedItemKind.Commitment => WorkItemOwner.Me,
            _ => null
        };
    }

    public static WorkItemOwner? ResolveOwner(string? legacyKind)
    {
        return TryParseKind(legacyKind, out var kind)
            ? ResolveOwner(kind)
            : null;
    }

    public static WorkItemOrigin? ResolveOrigin(ExtractedItemKind kind)
    {
        return kind switch
        {
            ExtractedItemKind.WaitingOn => WorkItemOrigin.Request,
            ExtractedItemKind.Commitment => WorkItemOrigin.Promise,
            ExtractedItemKind.Task => WorkItemOrigin.DetectedFromChat,
            ExtractedItemKind.Meeting => WorkItemOrigin.DetectedFromChat,
            _ => null
        };
    }

    public static WorkItemOrigin? ResolveOrigin(string? legacyKind)
    {
        return TryParseKind(legacyKind, out var kind)
            ? ResolveOrigin(kind)
            : null;
    }

    public static AiReviewState ResolveReviewState(double confidence)
    {
        return confidence switch
        {
            >= 0.90d => AiReviewState.Confirmed,
            <= 0.50d => AiReviewState.Rejected,
            _ => AiReviewState.NeedsReview
        };
    }

    public static WorkItemPriority ResolvePriority(
        string title,
        string summary,
        DateTimeOffset? targetAt,
        DateTimeOffset now)
    {
        var loweredTitle = title.Trim().ToLowerInvariant();
        var loweredSummary = summary.Trim().ToLowerInvariant();
        if (ContainsAny(loweredTitle, ImportantKeywords) || ContainsAny(loweredSummary, ImportantKeywords))
        {
            return WorkItemPriority.Important;
        }

        return targetAt is not null && targetAt <= now.AddHours(6)
            ? WorkItemPriority.Important
            : WorkItemPriority.Normal;
    }

    public static bool ResolveIsOverdue(
        WorkItemStatus? status,
        DateTimeOffset? dueAt,
        DateTimeOffset? plannedAt,
        DateTimeOffset now)
    {
        if (status is WorkItemStatus.Answered or
            WorkItemStatus.Missed or
            WorkItemStatus.Cancelled or
            WorkItemStatus.Completed or
            WorkItemStatus.Done or
            WorkItemStatus.NotRelevant)
        {
            return false;
        }

        var targetAt = plannedAt ?? dueAt;
        return targetAt is not null && targetAt < now;
    }

    private static string NormalizeKind(string? legacyKind)
    {
        return legacyKind?.Trim() ?? string.Empty;
    }

    private static bool TryParseKind(string? legacyKind, out ExtractedItemKind kind)
    {
        return Enum.TryParse(NormalizeKind(legacyKind), ignoreCase: false, out kind);
    }

    private static bool ContainsAny(string text, IEnumerable<string> values)
    {
        return values.Any(value => text.Contains(value, StringComparison.Ordinal));
    }
}

internal sealed record WorkItemMetadata(
    WorkItemType? Type,
    WorkItemStatus? Status,
    WorkItemPriority Priority,
    WorkItemOwner? Owner,
    WorkItemOrigin? Origin,
    AiReviewState ReviewState,
    DateTimeOffset? PlannedAt,
    DateTimeOffset? DueAt,
    WorkItemSource Source,
    DateTimeOffset UpdatedAt,
    bool IsOverdue,
    MeetingJoinProvider? MeetingProvider,
    Uri? MeetingJoinUrl);
