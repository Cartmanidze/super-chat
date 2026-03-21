namespace SuperChat.Domain.Features.Intelligence;

public sealed record WorkItemRecord(
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
    double Confidence,
    string? ResolutionKind = null,
    string? ResolutionSource = null,
    ResolutionTrace? ResolutionTrace = null,
    DateTimeOffset? ResolvedAt = null);
