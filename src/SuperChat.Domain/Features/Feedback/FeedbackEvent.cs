namespace SuperChat.Domain.Features.Feedback;

public sealed record FeedbackEvent(
    Guid Id,
    Guid UserId,
    string Area,
    string Value,
    string? Notes,
    DateTimeOffset CreatedAt);
