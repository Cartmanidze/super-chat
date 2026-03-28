using SuperChat.Domain.Features.Auth;
using SuperChat.Domain.Features.Integrations;

namespace SuperChat.Infrastructure.Features.Integrations;

public interface IIntegrationConnectionService
{
    Task<IReadOnlyList<IntegrationConnection>> GetConnectionsAsync(Guid userId, CancellationToken cancellationToken);

    Task<IntegrationConnection> GetStatusAsync(Guid userId, IntegrationProvider provider, CancellationToken cancellationToken);

    Task<IntegrationConnection> StartAsync(AppUser user, IntegrationProvider provider, CancellationToken cancellationToken);

    Task<IntegrationConnection> ReconnectAsync(AppUser user, IntegrationProvider provider, CancellationToken cancellationToken);

    Task<IntegrationConnection> StartChatLoginAsync(AppUser user, IntegrationProvider provider, CancellationToken cancellationToken);

    Task<IntegrationConnection> SubmitLoginInputAsync(AppUser user, IntegrationProvider provider, string input, CancellationToken cancellationToken);

    Task DisconnectAsync(Guid userId, IntegrationProvider provider, CancellationToken cancellationToken);

    Task<IReadOnlyList<IntegrationConnection>> GetReadyForDevelopmentSyncAsync(IntegrationProvider provider, CancellationToken cancellationToken);

    Task MarkSynchronizedAsync(
        Guid userId,
        IntegrationProvider provider,
        DateTimeOffset synchronizedAt,
        CancellationToken cancellationToken);
}
