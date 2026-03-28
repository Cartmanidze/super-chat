using SuperChat.Domain.Features.Auth;
using SuperChat.Domain.Features.Integrations.Telegram;

namespace SuperChat.Infrastructure.Features.Integrations.Telegram;

public interface ITelegramConnectionService
{
    Task<TelegramConnection> StartAsync(AppUser user, CancellationToken cancellationToken);

    Task<TelegramConnection> ReconnectAsync(AppUser user, CancellationToken cancellationToken);

    Task<TelegramConnection> StartChatLoginAsync(AppUser user, CancellationToken cancellationToken);

    Task<TelegramConnection> SubmitLoginInputAsync(AppUser user, string input, CancellationToken cancellationToken);

    Task<TelegramConnection> CompleteDevelopmentConnectionAsync(AppUser user, CancellationToken cancellationToken);

    Task<TelegramConnection> GetStatusAsync(Guid userId, CancellationToken cancellationToken);

    Task DisconnectAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TelegramConnection>> GetReadyForDevelopmentSyncAsync(CancellationToken cancellationToken);

    Task MarkSynchronizedAsync(Guid userId, DateTimeOffset synchronizedAt, CancellationToken cancellationToken);
}
