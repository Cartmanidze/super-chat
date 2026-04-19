using SuperChat.Domain.Features.Integrations;

namespace SuperChat.Api.Features.Me;

internal static class MeResponseMappings
{
    public static MeResponse ToMeResponse(
        this IntegrationConnection telegramConnection,
        Guid userId,
        string email)
    {
        return new MeResponse(
            Id: userId,
            Email: email,
            TelegramState: telegramConnection.State.ToString(),
            LastSyncedAt: telegramConnection.LastSyncedAt,
            RequiresTelegramAction: telegramConnection.State is not IntegrationConnectionState.Connected);
    }
}
