using SuperChat.Domain.Shared;

namespace SuperChat.Domain.Features.Auth;

public sealed record AppUser(
    Guid Id,
    Email Email,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt)
{
    private readonly bool _validated = Validate(Id);

    private static bool Validate(Guid id)
    {
        DomainGuard.NotEmpty(id);
        return true;
    }
}
