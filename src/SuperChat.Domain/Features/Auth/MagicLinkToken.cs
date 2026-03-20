namespace SuperChat.Domain.Features.Auth;

public sealed record MagicLinkToken(
    string Value,
    string Email,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    bool Consumed,
    Guid? ConsumedByUserId);
