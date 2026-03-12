namespace SuperChat.Domain.Model;

public sealed record IntegrationConnection(
    Guid UserId,
    IntegrationProvider Provider,
    IntegrationTransport Transport,
    IntegrationConnectionState State,
    Uri? ActionUrl,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastSyncedAt);
