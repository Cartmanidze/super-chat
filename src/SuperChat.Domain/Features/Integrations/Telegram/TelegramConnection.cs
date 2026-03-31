using SuperChat.Domain.Shared;

namespace SuperChat.Domain.Features.Integrations.Telegram;

public sealed record TelegramConnection(
    Guid UserId,
    TelegramConnectionState State,
    Uri? WebLoginUrl,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastSyncedAt)
{
    private readonly bool _validated = Validate(UserId);

    private static bool Validate(Guid userId)
    {
        DomainGuard.NotEmpty(userId);
        return true;
    }
}
