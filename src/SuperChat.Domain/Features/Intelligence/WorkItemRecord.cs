using SuperChat.Domain.Shared;

namespace SuperChat.Domain.Features.Intelligence;

public sealed record WorkItemRecord(
    Guid Id,
    Guid UserId,
    ExtractedItemKind Kind,
    string Title,
    string Summary,
    string ExternalChatId,
    string SourceEventId,
    string? Person,
    DateTimeOffset ObservedAt,
    DateTimeOffset? DueAt,
    Confidence Confidence,
    string? ResolutionKind = null,
    string? ResolutionSource = null,
    ResolutionTrace? ResolutionTrace = null,
    DateTimeOffset? ResolvedAt = null)
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
