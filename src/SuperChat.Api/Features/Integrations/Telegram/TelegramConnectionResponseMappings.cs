using SuperChat.Domain.Model;

namespace SuperChat.Api.Features.Integrations.Telegram;

internal static class TelegramConnectionResponseMappings
{
    public static TelegramConnectionResponse ToTelegramConnectionResponse(
        this IntegrationConnection connection,
        string? matrixUserId)
    {
        return new TelegramConnectionResponse(
            State: connection.State.ToString(),
            MatrixUserId: matrixUserId,
            WebLoginUrl: connection.ActionUrl,
            LastSyncedAt: connection.LastSyncedAt,
            RequiresAction: connection.State is not IntegrationConnectionState.Connected);
    }
}
