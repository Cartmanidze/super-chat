using SuperChat.Contracts.Features.WorkItems;

namespace SuperChat.Infrastructure.Features.Chat;

internal sealed record ChatResultItemProjection(
    Guid? Id,
    string Title,
    string Summary,
    string ChatTitle,
    DateTimeOffset? Timestamp,
    string? Kind = null,
    WorkItemType? Type = null,
    WorkItemStatus? Status = null,
    WorkItemPriority? Priority = null,
    WorkItemOwner? Owner = null,
    WorkItemOrigin? Origin = null,
    AiReviewState? ReviewState = null,
    DateTimeOffset? PlannedAt = null,
    DateTimeOffset? DueAt = null,
    WorkItemSource? Source = null,
    DateTimeOffset? UpdatedAt = null,
    bool IsOverdue = false,
    MeetingJoinProvider? MeetingProvider = null,
    Uri? MeetingJoinUrl = null);
