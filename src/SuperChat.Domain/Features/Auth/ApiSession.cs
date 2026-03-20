namespace SuperChat.Domain.Features.Auth;

public sealed record ApiSession(
    Guid UserId,
    string Token,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);
