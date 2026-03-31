using SuperChat.Domain.Shared;

namespace SuperChat.Domain.Features.Auth;

public sealed record ApiSession(
    Guid UserId,
    string Token,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt)
{
    private readonly bool _validated = Validate(UserId, Token);

    private static bool Validate(Guid userId, string token)
    {
        DomainGuard.NotEmpty(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return true;
    }
}
