namespace SuperChat.Domain.Model;

public sealed record TelegramConnection(
    Guid UserId,
    TelegramConnectionState State,
    Uri? WebLoginUrl,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastSyncedAt);
