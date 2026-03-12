using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

public sealed class TelegramConnectionService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    IMatrixProvisioningService matrixProvisioningService,
    MatrixApiClient matrixApiClient,
    IOptions<TelegramBridgeOptions> bridgeOptions,
    IOptions<PilotOptions> pilotOptions,
    TimeProvider timeProvider,
    ILogger<TelegramConnectionService> logger) : ITelegramConnectionService
{
    public async Task<TelegramConnection> StartAsync(AppUser user, CancellationToken cancellationToken)
    {
        if (pilotOptions.Value.DevSeedSampleData)
        {
            return await StartDevelopmentAsync(user, cancellationToken);
        }

        var identity = await matrixProvisioningService.EnsureIdentityAsync(user, cancellationToken);
        if (string.IsNullOrWhiteSpace(identity.AccessToken) || identity.AccessToken.StartsWith("dev-token-", StringComparison.Ordinal))
        {
            return await SetStateAsync(
                user.Id,
                TelegramConnectionState.RequiresSetup,
                webLoginUrl: null,
                managementRoomId: null,
                cancellationToken);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await FindOrCreateConnectionAsync(dbContext, user.Id, cancellationToken);

        if (entity.State == TelegramConnectionState.Connected &&
            !string.IsNullOrWhiteSpace(entity.ManagementRoomId) &&
            string.IsNullOrWhiteSpace(entity.WebLoginUrl))
        {
            return entity.ToDomain();
        }

        var managementRoomId = entity.ManagementRoomId;
        if (string.IsNullOrWhiteSpace(managementRoomId))
        {
            managementRoomId = await matrixApiClient.CreateDirectRoomAsync(
                identity.AccessToken,
                bridgeOptions.Value.BotUserId,
                cancellationToken);
        }

        await matrixApiClient.SendTextMessageAsync(identity.AccessToken, managementRoomId, "login", cancellationToken);

        entity.ManagementRoomId = managementRoomId;
        entity.State = TelegramConnectionState.BridgePending;
        entity.WebLoginUrl = null;
        entity.UpdatedAt = timeProvider.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Started Telegram bridge login for user {UserId} in room {RoomId}.", user.Id, managementRoomId);

        return entity.ToDomain();
    }

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

        var identity = await dbContext.MatrixIdentities.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (!pilotOptions.Value.DevSeedSampleData &&
            identity is not null &&
            !string.IsNullOrWhiteSpace(existing.ManagementRoomId) &&
            !string.IsNullOrWhiteSpace(identity.AccessToken) &&
            !identity.AccessToken.StartsWith("dev-token-", StringComparison.Ordinal))
        {
            try
            {
                await matrixApiClient.SendTextMessageAsync(identity.AccessToken, existing.ManagementRoomId, "logout", cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send logout command to Telegram bridge for user {UserId}.", userId);
            }
        }

        existing.State = TelegramConnectionState.Disconnected;
        existing.WebLoginUrl = null;
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

        if (pilotOptions.Value.DevSeedSampleData)
        {
            existing.DevelopmentSeededAt = synchronizedAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<TelegramConnection> StartDevelopmentAsync(AppUser user, CancellationToken cancellationToken)
    {
        var loginUrl = new Uri(bridgeOptions.Value.WebLoginBaseUrl.TrimEnd('/'));
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await FindOrCreateConnectionAsync(dbContext, user.Id, cancellationToken);
        existing.State = TelegramConnectionState.BridgePending;
        existing.WebLoginUrl = loginUrl.ToString();
        existing.UpdatedAt = timeProvider.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);
        return await CompleteDevelopmentConnectionAsync(user, cancellationToken);
    }

    private async Task<TelegramConnection> SetStateAsync(
        Guid userId,
        TelegramConnectionState state,
        string? webLoginUrl,
        string? managementRoomId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await FindOrCreateConnectionAsync(dbContext, userId, cancellationToken);
        existing.State = state;
        existing.WebLoginUrl = webLoginUrl;
        existing.ManagementRoomId = managementRoomId ?? existing.ManagementRoomId;
        existing.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return existing.ToDomain();
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
