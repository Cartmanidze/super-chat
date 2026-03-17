using SuperChat.Domain.Model;

namespace SuperChat.Api.Features.Integrations;

internal static class IntegrationConnectionResponseMappings
{
    public static IntegrationConnectionResponse ToIntegrationConnectionResponse(
        this IntegrationConnection connection,
        string? matrixUserId)
    {
        return new IntegrationConnectionResponse(
            Provider: connection.Provider.ToRouteSegment(),
            Transport: connection.Transport.ToString(),
            State: connection.State.ToString(),
            MatrixUserId: connection.Transport == IntegrationTransport.MatrixBridge ? matrixUserId : null,
            ActionUrl: connection.ActionUrl,
            LastSyncedAt: connection.LastSyncedAt,
            RequiresAction: connection.State is not IntegrationConnectionState.Connected);
    }
}
