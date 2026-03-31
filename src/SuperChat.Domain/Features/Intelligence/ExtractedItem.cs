using SuperChat.Domain.Shared;

namespace SuperChat.Domain.Features.Intelligence;

public sealed record ExtractedItem(
    Guid Id,
    Guid UserId,
    ExtractedItemKind Kind,
    string Title,
    string Summary,
    string SourceRoom,
    string SourceEventId,
    string? Person,
    DateTimeOffset ObservedAt,
    DateTimeOffset? DueAt,
    Confidence Confidence)
{
    private readonly bool _validated = Validate(Id, UserId, Title, Summary, SourceRoom, SourceEventId);

    private static bool Validate(Guid id, Guid userId, string title, string summary, string sourceRoom, string sourceEventId)
    {
        DomainGuard.NotEmpty(id);
        DomainGuard.NotEmpty(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoom);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEventId);
        return true;
    }
}
