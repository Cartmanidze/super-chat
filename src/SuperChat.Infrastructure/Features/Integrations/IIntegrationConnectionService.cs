using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Abstractions;

public interface IIntegrationConnectionService
{
    Task<IReadOnlyList<IntegrationConnection>> GetConnectionsAsync(Guid userId, CancellationToken cancellationToken);

    Task<IntegrationConnection> GetStatusAsync(Guid userId, IntegrationProvider provider, CancellationToken cancellationToken);

    Task<IntegrationConnection> StartAsync(AppUser user, IntegrationProvider provider, CancellationToken cancellationToken);

    Task DisconnectAsync(Guid userId, IntegrationProvider provider, CancellationToken cancellationToken);

    Task<IReadOnlyList<IntegrationConnection>> GetReadyForDevelopmentSyncAsync(IntegrationProvider provider, CancellationToken cancellationToken);

    Task MarkSynchronizedAsync(
        Guid userId,
        IntegrationProvider provider,
        DateTimeOffset synchronizedAt,
        CancellationToken cancellationToken);
}
