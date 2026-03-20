namespace SuperChat.Domain.Features.Integrations.Telegram;

public sealed record TelegramConnection(
    Guid UserId,
    TelegramConnectionState State,
    Uri? WebLoginUrl,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastSyncedAt);
