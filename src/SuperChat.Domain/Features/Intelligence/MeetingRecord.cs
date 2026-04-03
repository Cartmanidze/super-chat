using SuperChat.Domain.Shared;

namespace SuperChat.Domain.Features.Intelligence;

public sealed record MeetingRecord(
    Guid Id,
    Guid UserId,
    string Title,
    string Summary,
    string SourceRoom,
    string SourceEventId,
    string? Person,
    DateTimeOffset ObservedAt,
    DateTimeOffset ScheduledFor,
    Confidence Confidence,
    string? ResolutionKind = null,
    string? ResolutionSource = null,
    ResolutionTrace? ResolutionTrace = null,
    DateTimeOffset? ResolvedAt = null,
    string? MeetingProvider = null,
    Uri? MeetingJoinUrl = null,
    MeetingStatus Status = MeetingStatus.PendingConfirmation)
{
    private readonly bool _validated = Validate(Id, UserId, Title, Summary);

    private static bool Validate(Guid id, Guid userId, string title, string summary)
    {
        DomainGuard.NotEmpty(id);
        DomainGuard.NotEmpty(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        return true;
    }
}
