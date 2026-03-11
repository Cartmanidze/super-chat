namespace SuperChat.Domain.Model;

public sealed record ApiSession(
    Guid UserId,
    string Token,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);
