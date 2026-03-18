using SuperChat.Contracts.ViewModels;
using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Services;

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
        var kind = kindValue.ToString();
        var joinLink = kindValue == ExtractedItemKind.Meeting
            ? MeetingJoinLinkParser.TryParse(summary)
            : null;
        var plannedAt = kindValue == ExtractedItemKind.Meeting
            ? dueAt
            : null;

        var status = ResolveStatus(kind, summary);
        return new WorkItemMetadata(
            Type: ResolveType(kind),
            Status: status,
            Priority: ResolvePriority(title, summary, dueAt, now),
            Owner: ResolveOwner(kind),
            Origin: ResolveOrigin(kind),
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
        var status = ResolveStatus(nameof(ExtractedItemKind.Meeting), meeting.Summary);
        return new WorkItemMetadata(
            Type: WorkItemType.Event,
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

    public static WorkItemType? ResolveType(string? legacyKind)
    {
        return NormalizeKind(legacyKind) switch
        {
            nameof(ExtractedItemKind.WaitingOn) => WorkItemType.Request,
            nameof(ExtractedItemKind.Meeting) => WorkItemType.Event,
            nameof(ExtractedItemKind.Task) => WorkItemType.ActionItem,
            nameof(ExtractedItemKind.Commitment) => WorkItemType.ActionItem,
            _ => null
        };
    }

    public static WorkItemStatus? ResolveStatus(string? legacyKind, string summary)
    {
        var normalizedKind = NormalizeKind(legacyKind);
        var loweredSummary = summary.Trim().ToLowerInvariant();

        return normalizedKind switch
        {
            nameof(ExtractedItemKind.WaitingOn) => WorkItemStatus.AwaitingResponse,
            nameof(ExtractedItemKind.Task) => WorkItemStatus.ToDo,
            nameof(ExtractedItemKind.Commitment) => WorkItemStatus.ToDo,
            nameof(ExtractedItemKind.Meeting) when ContainsAny(loweredSummary, MeetingCancellationKeywords) => WorkItemStatus.Cancelled,
            nameof(ExtractedItemKind.Meeting) when ContainsAny(loweredSummary, MeetingRescheduleKeywords) => WorkItemStatus.Rescheduled,
            nameof(ExtractedItemKind.Meeting) when ContainsAny(loweredSummary, MeetingConfirmationKeywords) => WorkItemStatus.Confirmed,
            nameof(ExtractedItemKind.Meeting) => WorkItemStatus.PendingConfirmation,
            _ => null
        };
    }

    public static WorkItemOwner? ResolveOwner(string? legacyKind)
    {
        return NormalizeKind(legacyKind) switch
        {
            nameof(ExtractedItemKind.WaitingOn) => WorkItemOwner.Contact,
            nameof(ExtractedItemKind.Meeting) => WorkItemOwner.Both,
            nameof(ExtractedItemKind.Task) => WorkItemOwner.Me,
            nameof(ExtractedItemKind.Commitment) => WorkItemOwner.Me,
            _ => null
        };
    }

    public static WorkItemOrigin? ResolveOrigin(string? legacyKind)
    {
        return NormalizeKind(legacyKind) switch
        {
            nameof(ExtractedItemKind.WaitingOn) => WorkItemOrigin.Request,
            nameof(ExtractedItemKind.Commitment) => WorkItemOrigin.Promise,
            nameof(ExtractedItemKind.Task) => WorkItemOrigin.DetectedFromChat,
            nameof(ExtractedItemKind.Meeting) => WorkItemOrigin.DetectedFromChat,
            _ => null
        };
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
