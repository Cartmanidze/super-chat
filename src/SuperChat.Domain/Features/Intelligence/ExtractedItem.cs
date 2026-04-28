using SuperChat.Domain.Shared;

namespace SuperChat.Domain.Features.Intelligence;

public sealed record ExtractedItem(
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
    Confidence Confidence)
{
    private readonly bool _validated = Validate(Id, UserId, Title, Summary, ExternalChatId, SourceEventId);

    private static bool Validate(Guid id, Guid userId, string title, string summary, string externalChatId, string sourceEventId)
    {
        DomainGuard.NotEmpty(id);
        DomainGuard.NotEmpty(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalChatId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEventId);
        return true;
    }
}
