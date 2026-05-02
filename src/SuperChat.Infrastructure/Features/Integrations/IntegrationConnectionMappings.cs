using SuperChat.Domain.Features.Integrations;
using SuperChat.Domain.Features.Integrations.Telegram;

namespace SuperChat.Infrastructure.Features.Integrations;

internal static class IntegrationConnectionMappings
{
    public static IntegrationConnection ToIntegrationConnection(this TelegramConnection connection)
    {
        return new IntegrationConnection(
            connection.UserId,
            IntegrationProvider.Telegram,
            IntegrationProvider.Telegram.GetDefaultTransport(),
            connection.State.ToIntegrationConnectionState(),
            ActionUrl: null,
            connection.UpdatedAt,
            connection.LastSyncedAt,
            connection.State.ToChatLoginStep());
    }

    public static IntegrationConnectionState ToIntegrationConnectionState(this TelegramConnectionState state)
    {
        return state switch
        {
            TelegramConnectionState.NotStarted => IntegrationConnectionState.NotStarted,
            TelegramConnectionState.BridgePending => IntegrationConnectionState.Pending,
            TelegramConnectionState.Connected => IntegrationConnectionState.Connected,
            TelegramConnectionState.RequiresSetup => IntegrationConnectionState.RequiresSetup,
            TelegramConnectionState.Disconnected => IntegrationConnectionState.Disconnected,
            // Revoked = Telegram отозвал auth_key. Внешнему контракту IntegrationConnectionState
            // отдельного значения нет — мапим в Disconnected, чтобы UI показал «нужен вход».
            // Внутренний enum TelegramConnectionState отличает Revoked от Disconnected
            // (для аналитики и логов), наружу разница не уезжает.
            TelegramConnectionState.Revoked => IntegrationConnectionState.Disconnected,
            TelegramConnectionState.Error => IntegrationConnectionState.Error,
            TelegramConnectionState.LoginAwaitingPhone => IntegrationConnectionState.Pending,
            TelegramConnectionState.LoginAwaitingCode => IntegrationConnectionState.Pending,
            TelegramConnectionState.LoginAwaitingPassword => IntegrationConnectionState.Pending,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }

    public static string? ToChatLoginStep(this TelegramConnectionState state)
    {
        return state switch
        {
            TelegramConnectionState.LoginAwaitingPhone => "phone",
            TelegramConnectionState.LoginAwaitingCode => "code",
            TelegramConnectionState.LoginAwaitingPassword => "password",
            _ => null
        };
    }
}
