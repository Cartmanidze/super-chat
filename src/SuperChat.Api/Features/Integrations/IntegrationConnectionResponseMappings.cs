using SuperChat.Domain.Features.Integrations;

namespace SuperChat.Api.Features.Integrations;

internal static class IntegrationConnectionResponseMappings
{
    public static IntegrationConnectionResponse ToIntegrationConnectionResponse(this IntegrationConnection connection)
    {
        return new IntegrationConnectionResponse(
            Provider: connection.Provider.ToRouteSegment(),
            Transport: connection.Transport.ToString(),
            State: connection.State.ToString(),
            ActionUrl: connection.ActionUrl,
            LastSyncedAt: connection.LastSyncedAt,
            RequiresAction: connection.State is not IntegrationConnectionState.Connected);
    }
}
