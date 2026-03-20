namespace SuperChat.Domain.Features.Auth;

public sealed record AppUser(
    Guid Id,
    string Email,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt);
