using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Abstractions;

public interface ITelegramConnectionService
{
    Task<TelegramConnection> StartAsync(AppUser user, CancellationToken cancellationToken);
    Task<TelegramConnection> CompleteDevelopmentConnectionAsync(AppUser user, CancellationToken cancellationToken);

    Task<TelegramConnection> GetStatusAsync(Guid userId, CancellationToken cancellationToken);

    Task DisconnectAsync(Guid userId, CancellationToken cancellationToken);
}
