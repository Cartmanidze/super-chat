namespace SuperChat.Domain.Features.Integrations;

public sealed record IntegrationConnection(
    Guid UserId,
    IntegrationProvider Provider,
    IntegrationTransport Transport,
    IntegrationConnectionState State,
    Uri? ActionUrl,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastSyncedAt,
    string? ChatLoginStep = null);
