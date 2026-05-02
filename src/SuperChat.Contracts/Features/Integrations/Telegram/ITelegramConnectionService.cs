using SuperChat.Domain.Features.Auth;
using SuperChat.Domain.Features.Integrations.Telegram;

namespace SuperChat.Contracts.Features.Integrations.Telegram;

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

    /// <summary>
    /// Помечает соединение как отозванное со стороны Telegram (sidecar получил
    /// is_user_authorized() == false при resume или health-check). Не делает
    /// сетевых вызовов в sidecar — только обновляет state в БД, чтобы UI
    /// показал «нужен вход». Идемпотентно: повторные вызовы для уже Revoked
    /// просто обновляют UpdatedAt.
    /// </summary>
    Task MarkRevokedAsync(Guid userId, string reason, CancellationToken cancellationToken);
}
