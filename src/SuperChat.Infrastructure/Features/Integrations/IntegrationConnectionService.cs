using SuperChat.Domain.Features.Auth;
using SuperChat.Domain.Features.Integrations;
using SuperChat.Infrastructure.Features.Integrations.Telegram;

namespace SuperChat.Infrastructure.Features.Integrations;

public sealed class IntegrationConnectionService(
    ITelegramConnectionService telegramConnectionService) : IIntegrationConnectionService
{
    public async Task<IReadOnlyList<IntegrationConnection>> GetConnectionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var telegram = await telegramConnectionService.GetStatusAsync(userId, cancellationToken);
        return [telegram.ToIntegrationConnection()];
    }

    public async Task<IntegrationConnection> GetStatusAsync(
        Guid userId,
        IntegrationProvider provider,
        CancellationToken cancellationToken)
    {
        return provider switch
        {
            IntegrationProvider.Telegram => (await telegramConnectionService.GetStatusAsync(userId, cancellationToken))
                .ToIntegrationConnection(),
            _ => throw CreateUnsupportedProviderException(provider)
        };
    }

    public async Task<IntegrationConnection> StartAsync(
        AppUser user,
        IntegrationProvider provider,
        CancellationToken cancellationToken)
    {
        return provider switch
        {
            IntegrationProvider.Telegram => (await telegramConnectionService.StartAsync(user, cancellationToken))
                .ToIntegrationConnection(),
            _ => throw CreateUnsupportedProviderException(provider)
        };
    }

    public Task DisconnectAsync(Guid userId, IntegrationProvider provider, CancellationToken cancellationToken)
    {
        return provider switch
        {
            IntegrationProvider.Telegram => telegramConnectionService.DisconnectAsync(userId, cancellationToken),
            _ => throw CreateUnsupportedProviderException(provider)
        };
    }

    public async Task<IReadOnlyList<IntegrationConnection>> GetReadyForDevelopmentSyncAsync(
        IntegrationProvider provider,
        CancellationToken cancellationToken)
    {
        return provider switch
        {
            IntegrationProvider.Telegram => (await telegramConnectionService.GetReadyForDevelopmentSyncAsync(cancellationToken))
                .Select(item => item.ToIntegrationConnection())
                .ToList(),
            _ => throw CreateUnsupportedProviderException(provider)
        };
    }

    public Task MarkSynchronizedAsync(
        Guid userId,
        IntegrationProvider provider,
        DateTimeOffset synchronizedAt,
        CancellationToken cancellationToken)
    {
        return provider switch
        {
            IntegrationProvider.Telegram => telegramConnectionService.MarkSynchronizedAsync(
                userId,
                synchronizedAt,
                cancellationToken),
            _ => throw CreateUnsupportedProviderException(provider)
        };
    }

    private static NotSupportedException CreateUnsupportedProviderException(IntegrationProvider provider)
    {
        return new($"Integration provider '{provider}' is not implemented yet.");
    }
}
