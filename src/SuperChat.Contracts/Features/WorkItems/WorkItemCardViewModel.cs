using System.Text.Json.Serialization;

namespace SuperChat.Contracts.Features.WorkItems;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "modelType")]
[JsonDerivedType(typeof(MeetingWorkItemCardViewModel), "meeting")]
public record WorkItemCardViewModel(
    Guid? Id,
    string Title,
    string Summary,
    DateTimeOffset ObservedAt,
    DateTimeOffset? DueAt,
    string ChatTitle,
    double Confidence = 0d,
    WorkItemType? Type = null,
    WorkItemStatus? Status = null,
    WorkItemPriority Priority = WorkItemPriority.Normal,
    WorkItemOwner? Owner = null,
    WorkItemOrigin Origin = WorkItemOrigin.DetectedFromChat,
    AiReviewState ReviewState = AiReviewState.NeedsReview,
    DateTimeOffset? PlannedAt = null,
    WorkItemSource Source = WorkItemSource.Telegram,
    DateTimeOffset? UpdatedAt = null,
    bool IsOverdue = false,
    MeetingJoinProvider? MeetingProvider = null,
    Uri? MeetingJoinUrl = null)
{
    public WorkItemCardViewModel(
        string Title,
        string Summary,
        DateTimeOffset ObservedAt,
        DateTimeOffset? DueAt,
        string ChatTitle,
        double Confidence = 0d,
        WorkItemType? Type = null,
        WorkItemStatus? Status = null,
        WorkItemPriority Priority = WorkItemPriority.Normal,
        WorkItemOwner? Owner = null,
        WorkItemOrigin Origin = WorkItemOrigin.DetectedFromChat,
        AiReviewState ReviewState = AiReviewState.NeedsReview,
        DateTimeOffset? PlannedAt = null,
        WorkItemSource Source = WorkItemSource.Telegram,
        DateTimeOffset? UpdatedAt = null,
        bool IsOverdue = false,
        MeetingJoinProvider? MeetingProvider = null,
        Uri? MeetingJoinUrl = null)
        : this(
            null,
            Title,
            Summary,
            ObservedAt,
            DueAt,
            ChatTitle,
            Confidence,
            Type,
            Status,
            Priority,
            Owner,
            Origin,
            ReviewState,
            PlannedAt,
            Source,
            UpdatedAt,
            IsOverdue,
            MeetingProvider,
            MeetingJoinUrl)
    {
    }
}

public sealed record MeetingWorkItemCardViewModel(
    string Title,
    string Summary,
    DateTimeOffset ObservedAt,
    DateTimeOffset? DueAt,
    string ChatTitle,
    MeetingStatus MeetingStatus = MeetingStatus.PendingConfirmation,
    double Confidence = 0d,
    WorkItemPriority Priority = WorkItemPriority.Normal,
    WorkItemOwner? Owner = null,
    WorkItemOrigin Origin = WorkItemOrigin.DetectedFromChat,
    AiReviewState ReviewState = AiReviewState.NeedsReview,
    DateTimeOffset? PlannedAt = null,
    WorkItemSource Source = WorkItemSource.Telegram,
    DateTimeOffset? UpdatedAt = null,
    bool IsOverdue = false,
    MeetingJoinProvider? MeetingProvider = null,
    Uri? MeetingJoinUrl = null)
    : WorkItemCardViewModel(
        null,
        Title,
        Summary,
        ObservedAt,
        DueAt,
        ChatTitle,
        Confidence,
        WorkItemType.Meeting,
        MeetingStatus.ToWorkItemStatus(),
        Priority,
        Owner,
        Origin,
        ReviewState,
        PlannedAt,
        Source,
        UpdatedAt,
        IsOverdue,
        MeetingProvider,
        MeetingJoinUrl);
