namespace SuperChat.Domain.Features.Integrations.Telegram;

public interface ITelegramConnectionRepository
{
    Task<TelegramConnection?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task SaveAsync(TelegramConnection connection, CancellationToken cancellationToken);
    Task<IReadOnlyList<TelegramConnection>> GetConnectedUnsyncedAsync(CancellationToken cancellationToken);
}
