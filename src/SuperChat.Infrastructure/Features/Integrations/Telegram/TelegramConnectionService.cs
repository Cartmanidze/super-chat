using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Contracts.Features.Integrations.Telegram;
using SuperChat.Domain.Features.Auth;
using SuperChat.Domain.Features.Integrations.Telegram;
using SuperChat.Infrastructure.Features.Integrations.Matrix;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Integrations.Telegram;

public sealed class TelegramConnectionService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    IMatrixProvisioningService matrixProvisioningService,
    MatrixApiClient matrixApiClient,
    IOptions<TelegramBridgeOptions> bridgeOptions,
    IOptions<PilotOptions> pilotOptions,
    TimeProvider timeProvider,
    ILogger<TelegramConnectionService> logger) : ITelegramConnectionService
{
    internal static readonly TimeSpan BridgeBotJoinTimeout = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan BridgeBotJoinPollInterval = TimeSpan.FromMilliseconds(150);
    internal static readonly TimeSpan BridgeLoginRefreshSkew = TimeSpan.FromSeconds(30);

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

        entity.ManagementRoomId = managementRoomId;
        entity.State = TelegramConnectionState.BridgePending;
        entity.WebLoginUrl = null;
        entity.UpdatedAt = timeProvider.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);

        var bridgeBotReady = await WaitForBridgeBotJoinAsync(identity.AccessToken, managementRoomId, cancellationToken);
        if (bridgeBotReady)
        {
            await matrixApiClient.SendTextMessageAsync(identity.AccessToken, managementRoomId, "login", cancellationToken);
            logger.LogInformation("Started Telegram bridge login for user {UserId} in room {RoomId}.", user.Id, managementRoomId);
        }
        else
        {
            logger.LogWarning(
                "Telegram bridge bot {BotUserId} did not join room {RoomId} for user {UserId} before timeout. Leaving connection pending until the room becomes ready.",
                bridgeOptions.Value.BotUserId,
                managementRoomId,
                user.Id);
        }

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
        await RefreshExpiredBridgeLoginAsync(dbContext, existing, cancellationToken);
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

    private async Task RefreshExpiredBridgeLoginAsync(
        SuperChatDbContext dbContext,
        TelegramConnectionEntity connection,
        CancellationToken cancellationToken)
    {
        if (pilotOptions.Value.DevSeedSampleData ||
            !ShouldRefreshExpiredBridgeLogin(connection.State, connection.WebLoginUrl, timeProvider.GetUtcNow()))
        {
            return;
        }

        connection.WebLoginUrl = null;
        connection.UpdatedAt = timeProvider.GetUtcNow();

        if (string.IsNullOrWhiteSpace(connection.ManagementRoomId))
        {
            logger.LogWarning(
                "Cleared expired Telegram bridge login URL for user {UserId}, but no management room is known.",
                connection.UserId);
            return;
        }

        var identity = await dbContext.MatrixIdentities
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == connection.UserId, cancellationToken);
        if (identity is null ||
            string.IsNullOrWhiteSpace(identity.AccessToken) ||
            identity.AccessToken.StartsWith("dev-token-", StringComparison.Ordinal))
        {
            logger.LogWarning(
                "Cleared expired Telegram bridge login URL for user {UserId}, but no live Matrix access token is available.",
                connection.UserId);
            return;
        }

        try
        {
            var bridgeBotReady = await WaitForBridgeBotJoinAsync(identity.AccessToken, connection.ManagementRoomId, cancellationToken);
            if (!bridgeBotReady)
            {
                logger.LogWarning(
                    "Telegram bridge bot {BotUserId} did not join room {RoomId} for user {UserId} while refreshing an expired login URL.",
                    bridgeOptions.Value.BotUserId,
                    connection.ManagementRoomId,
                    connection.UserId);
                return;
            }

            await matrixApiClient.SendTextMessageAsync(identity.AccessToken, connection.ManagementRoomId, "login", cancellationToken);
            logger.LogInformation(
                "Re-issued Telegram bridge login for user {UserId} in room {RoomId} because the stored login URL expired.",
                connection.UserId,
                connection.ManagementRoomId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to refresh expired Telegram bridge login URL for user {UserId} in room {RoomId}.",
                connection.UserId,
                connection.ManagementRoomId);
        }
    }

    private async Task<bool> WaitForBridgeBotJoinAsync(
        string accessToken,
        string managementRoomId,
        CancellationToken cancellationToken)
    {
        var deadline = timeProvider.GetUtcNow().Add(BridgeBotJoinTimeout);
        while (true)
        {
            var membership = await matrixApiClient.GetRoomMembershipAsync(
                accessToken,
                managementRoomId,
                bridgeOptions.Value.BotUserId,
                cancellationToken);
            if (string.Equals(membership, "join", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var remaining = deadline - timeProvider.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
            {
                return false;
            }

            await Task.Delay(
                remaining < BridgeBotJoinPollInterval ? remaining : BridgeBotJoinPollInterval,
                cancellationToken);
        }
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

    internal static bool ShouldRefreshExpiredBridgeLogin(
        TelegramConnectionState state,
        string? webLoginUrl,
        DateTimeOffset now)
    {
        if (state != TelegramConnectionState.BridgePending &&
            state != TelegramConnectionState.Error)
        {
            return false;
        }

        return TryGetBridgeLoginExpiry(webLoginUrl) is DateTimeOffset expiry &&
               expiry <= now.Add(BridgeLoginRefreshSkew);
    }

    internal static DateTimeOffset? TryGetBridgeLoginExpiry(string? webLoginUrl)
    {
        if (string.IsNullOrWhiteSpace(webLoginUrl) ||
            !Uri.TryCreate(webLoginUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var token = TryGetQueryParameter(uri, "token");
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var separatorIndex = token.IndexOf(':', StringComparison.Ordinal);
        if (separatorIndex < 0 || separatorIndex == token.Length - 1)
        {
            return null;
        }

        var payload = token[(separatorIndex + 1)..];
        if (!TryDecodeBase64Url(payload, out var jsonBytes))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(jsonBytes);
            if (!document.RootElement.TryGetProperty("expiry", out var expiryElement) ||
                !expiryElement.TryGetInt64(out var expiryUnixSeconds))
            {
                return null;
            }

            return DateTimeOffset.FromUnixTimeSeconds(expiryUnixSeconds);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string? TryGetQueryParameter(Uri uri, string key)
    {
        var query = uri.Query;
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        foreach (var segment in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = segment.Split('=', 2);
            if (parts.Length == 0 ||
                !string.Equals(Uri.UnescapeDataString(parts[0]), key, StringComparison.Ordinal))
            {
                continue;
            }

            return parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
        }

        return null;
    }

    private static bool TryDecodeBase64Url(string value, out byte[] bytes)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized = (normalized.Length % 4) switch
        {
            0 => normalized,
            2 => normalized + "==",
            3 => normalized + "=",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(normalized))
        {
            bytes = [];
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(normalized);
            return true;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }
}
