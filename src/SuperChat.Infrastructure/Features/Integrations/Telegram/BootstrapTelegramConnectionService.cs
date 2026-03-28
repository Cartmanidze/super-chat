using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Contracts.Features.Integrations.Telegram;
using SuperChat.Domain.Features.Auth;
using SuperChat.Domain.Features.Integrations.Telegram;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Integrations.Telegram;

public sealed class BootstrapTelegramConnectionService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    IOptions<TelegramBridgeOptions> bridgeOptions,
    IOptions<PilotOptions> pilotOptions,
    TimeProvider timeProvider) : ITelegramConnectionService
{
    public async Task<TelegramConnection> StartAsync(AppUser user, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var options = bridgeOptions.Value;
        var loginUrl = new Uri(options.WebLoginBaseUrl.TrimEnd('/'));
        var connection = new TelegramConnectionEntity
        {
            UserId = user.Id,
            State = TelegramConnectionState.BridgePending,
            WebLoginUrl = loginUrl.ToString(),
            UpdatedAt = now
        };

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await dbContext.TelegramConnections.SingleOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        if (existing is null)
        {
            dbContext.TelegramConnections.Add(connection);
        }
        else
        {
            existing.State = connection.State;
            existing.WebLoginUrl = connection.WebLoginUrl;
            existing.UpdatedAt = connection.UpdatedAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (pilotOptions.Value.DevSeedSampleData)
        {
            return await CompleteDevelopmentConnectionAsync(user, cancellationToken);
        }

        return connection.ToDomain();
    }

    public async Task<TelegramConnection> ReconnectAsync(AppUser user, CancellationToken cancellationToken)
    {
        await DisconnectAsync(user.Id, cancellationToken);
        return await StartAsync(user, cancellationToken);
    }

    public Task<TelegramConnection> StartChatLoginAsync(AppUser user, CancellationToken cancellationToken)
        => StartAsync(user, cancellationToken);

    public Task<TelegramConnection> SubmitLoginInputAsync(AppUser user, string input, CancellationToken cancellationToken)
        => GetStatusAsync(user.Id, cancellationToken);

    public async Task<TelegramConnection> CompleteDevelopmentConnectionAsync(AppUser user, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await FindOrCreateConnectionAsync(dbContext, user.Id, cancellationToken);
        existing.State = TelegramConnectionState.Connected;
        existing.UpdatedAt = timeProvider.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);
        return existing.ToDomain();
    }

    public async Task<TelegramConnection> GetStatusAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await FindOrCreateConnectionAsync(dbContext, userId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return existing.ToDomain();
    }

    public async Task DisconnectAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await FindOrCreateConnectionAsync(dbContext, userId, cancellationToken);
        existing.State = TelegramConnectionState.Disconnected;
        existing.UpdatedAt = timeProvider.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TelegramConnection>> GetReadyForDevelopmentSyncAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var connections = await dbContext.TelegramConnections
            .AsNoTracking()
            .Where(item => item.State == TelegramConnectionState.Connected && item.DevelopmentSeededAt == null)
            .OrderBy(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);

        return connections.Select(item => item.ToDomain()).ToList();
    }

    public async Task MarkSynchronizedAsync(Guid userId, DateTimeOffset synchronizedAt, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await FindOrCreateConnectionAsync(dbContext, userId, cancellationToken);
        existing.LastSyncedAt = synchronizedAt;
        existing.UpdatedAt = synchronizedAt;
        existing.DevelopmentSeededAt = synchronizedAt;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<TelegramConnectionEntity> FindOrCreateConnectionAsync(
        SuperChatDbContext dbContext,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.TelegramConnections.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var connection = new TelegramConnectionEntity
        {
            UserId = userId,
            State = TelegramConnectionState.NotStarted,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.TelegramConnections.Add(connection);
        return connection;
    }
}
