namespace SuperChat.Domain.Model;

public sealed record FeedbackEvent(
    Guid Id,
    Guid UserId,
    string Area,
    string Value,
    string? Notes,
    DateTimeOffset CreatedAt);
