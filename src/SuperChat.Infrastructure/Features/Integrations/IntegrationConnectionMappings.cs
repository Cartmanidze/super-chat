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
            connection.WebLoginUrl,
            connection.UpdatedAt,
            connection.LastSyncedAt);
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
            TelegramConnectionState.Error => IntegrationConnectionState.Error,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }
}
