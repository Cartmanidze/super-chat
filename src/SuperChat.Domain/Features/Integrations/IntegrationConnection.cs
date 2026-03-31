using SuperChat.Domain.Shared;

namespace SuperChat.Domain.Features.Integrations;

public sealed record IntegrationConnection(
    Guid UserId,
    IntegrationProvider Provider,
    IntegrationTransport Transport,
    IntegrationConnectionState State,
    Uri? ActionUrl,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastSyncedAt,
    string? ChatLoginStep = null)
{
    private readonly bool _validated = Validate(UserId);

    private static bool Validate(Guid userId)
    {
        DomainGuard.NotEmpty(userId);
        return true;
    }
}
