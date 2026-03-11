namespace SuperChat.Domain.Model;

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
    double Confidence);
