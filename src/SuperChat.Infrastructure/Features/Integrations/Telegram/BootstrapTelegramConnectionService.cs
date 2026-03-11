using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.State;

namespace SuperChat.Infrastructure.Services;

public sealed class BootstrapTelegramConnectionService(
    SuperChatStore store,
    IOptions<TelegramBridgeOptions> bridgeOptions,
    IOptions<PilotOptions> pilotOptions,
    TimeProvider timeProvider) : ITelegramConnectionService
{
    public async Task<TelegramConnection> StartAsync(AppUser user, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var options = bridgeOptions.Value;
        var loginUrl = new Uri($"{options.WebLoginBaseUrl.TrimEnd('/')}/?user={Uri.EscapeDataString(user.Email)}");
        var connection = new TelegramConnection(user.Id, TelegramConnectionState.BridgePending, loginUrl, now, null);

        store.UpsertConnection(connection);

        if (pilotOptions.Value.DevSeedSampleData)
        {
            return await CompleteDevelopmentConnectionAsync(user, cancellationToken);
        }

        return connection;
    }

    public Task<TelegramConnection> CompleteDevelopmentConnectionAsync(AppUser user, CancellationToken cancellationToken)
    {
        var existing = store.GetConnection(user.Id);
        var connection = existing with
        {
            State = TelegramConnectionState.Connected,
            UpdatedAt = timeProvider.GetUtcNow()
        };

        store.UpsertConnection(connection);
        return Task.FromResult(connection);
    }

    public Task<TelegramConnection> GetStatusAsync(Guid userId, CancellationToken cancellationToken)
    {
        return Task.FromResult(store.GetConnection(userId));
    }

    public Task DisconnectAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existing = store.GetConnection(userId);
        store.UpsertConnection(existing with
        {
            State = TelegramConnectionState.Disconnected,
            UpdatedAt = timeProvider.GetUtcNow(),
            LastSyncedAt = existing.LastSyncedAt
        });

        return Task.CompletedTask;
    }
}
