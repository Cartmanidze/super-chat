using SuperChat.Contracts.Features.Integrations.Telegram;
using SuperChat.Domain.Features.Auth;
using SuperChat.Domain.Features.Integrations;
using SuperChat.Domain.Features.Integrations.Telegram;
using SuperChat.Infrastructure.Features.Integrations;

namespace SuperChat.Tests;

public sealed class IntegrationConnectionServiceTests
{
    [Fact]
    public async Task GetStatusAsync_MapsTelegramConnectionToGenericIntegration()
    {
        var expected = new TelegramConnection(
            Guid.NewGuid(),
            TelegramConnectionState.BridgePending,
            new Uri("https://bridge.localhost/public/login"),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(-5));

        var service = new IntegrationConnectionService(new FakeTelegramConnectionService(expected));

        var connection = await service.GetStatusAsync(expected.UserId, IntegrationProvider.Telegram, CancellationToken.None);

        Assert.Equal(IntegrationProvider.Telegram, connection.Provider);
        Assert.Equal(IntegrationTransport.MatrixBridge, connection.Transport);
        Assert.Equal(IntegrationConnectionState.Pending, connection.State);
        Assert.Equal(expected.WebLoginUrl, connection.ActionUrl);
        Assert.Equal(expected.LastSyncedAt, connection.LastSyncedAt);
    }

    [Fact]
    public async Task GetStatusAsync_ThrowsForUnsupportedProvider()
    {
        var expected = new TelegramConnection(
            Guid.NewGuid(),
            TelegramConnectionState.NotStarted,
            null,
            DateTimeOffset.UtcNow,
            null);

        var service = new IntegrationConnectionService(new FakeTelegramConnectionService(expected));

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            service.GetStatusAsync(expected.UserId, IntegrationProvider.Email, CancellationToken.None));
    }

    [Fact]
    public async Task ReconnectAsync_MapsTelegramConnectionToGenericIntegration()
    {
        var user = new AppUser(Guid.NewGuid(), new Email("pilot@example.com"), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var expected = new TelegramConnection(
            user.Id,
            TelegramConnectionState.BridgePending,
            new Uri("https://bridge.localhost/public/login"),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(-5));

        var service = new IntegrationConnectionService(new FakeTelegramConnectionService(expected));

        var connection = await service.ReconnectAsync(user, IntegrationProvider.Telegram, CancellationToken.None);

        Assert.Equal(user.Id, connection.UserId);
        Assert.Equal(IntegrationProvider.Telegram, connection.Provider);
        Assert.Equal(IntegrationConnectionState.Pending, connection.State);
        Assert.Equal(expected.WebLoginUrl, connection.ActionUrl);
    }

    private sealed class FakeTelegramConnectionService(TelegramConnection connection) : ITelegramConnectionService
    {
        public Task<TelegramConnection> StartAsync(AppUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(connection with { UserId = user.Id });
        }

        public Task<TelegramConnection> ReconnectAsync(AppUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(connection with { UserId = user.Id });
        }

        public Task<TelegramConnection> StartChatLoginAsync(AppUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(connection with { UserId = user.Id });
        }

        public Task<TelegramConnection> SubmitLoginInputAsync(AppUser user, string input, CancellationToken cancellationToken)
        {
            return Task.FromResult(connection with { UserId = user.Id });
        }

        public Task<TelegramConnection> CompleteDevelopmentConnectionAsync(AppUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(connection);
        }

        public Task<TelegramConnection> GetStatusAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(connection with { UserId = userId });
        }

        public Task DisconnectAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TelegramConnection>> GetReadyForDevelopmentSyncAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<TelegramConnection>>([connection]);
        }

        public Task MarkSynchronizedAsync(Guid userId, DateTimeOffset synchronizedAt, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
