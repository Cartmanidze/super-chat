using SuperChat.Domain.Features.Integrations;

namespace SuperChat.Api.Features.Integrations.Telegram;

internal static class TelegramConnectionResponseMappings
{
    public static TelegramConnectionResponse ToTelegramConnectionResponse(this IntegrationConnection connection)
    {
        return new TelegramConnectionResponse(
            State: connection.State.ToString(),
            ChatLoginStep: connection.ChatLoginStep,
            LastSyncedAt: connection.LastSyncedAt,
            RequiresAction: connection.State is not IntegrationConnectionState.Connected);
    }
}
