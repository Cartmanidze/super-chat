namespace SuperChat.Domain.Model;

public sealed record AppUser(
    Guid Id,
    string Email,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt);
