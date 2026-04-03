using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Contracts.Features.Integrations.Matrix;
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
    // BridgeBotJoinTimeout is now configurable via TelegramBridgeOptions.BridgeBotJoinTimeoutSeconds (default: 15s)
    internal static readonly TimeSpan BridgeBotJoinPollInterval = TimeSpan.FromMilliseconds(150);
    internal static readonly TimeSpan BridgeLoginRefreshSkew = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan LoginRetryDelay = TimeSpan.FromSeconds(2);

    public async Task<TelegramConnection> StartAsync(AppUser user, CancellationToken cancellationToken)
    {
        if (pilotOptions.Value.DevSeedSampleData)
        {
            return await StartDevelopmentAsync(user, cancellationToken);
        }

        var identity = await matrixProvisioningService.EnsureIdentityAsync(user, cancellationToken);
        if (!IsLiveAccessToken(identity.AccessToken))
        {
            return await SetStateAsync(user.Id, TelegramConnectionState.RequiresSetup, null, null, cancellationToken);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await FindOrCreateConnectionAsync(dbContext, user.Id, cancellationToken);

        // If already connected, send logout first. The bridge processes logout
        // asynchronously — we persist Disconnected immediately so a retry skips
        // the logout phase and goes straight to login.
        if (entity.State == TelegramConnectionState.Connected &&
            !string.IsNullOrWhiteSpace(entity.ManagementRoomId))
        {
            try
            {
                await matrixApiClient.SendTextMessageAsync(identity.AccessToken, entity.ManagementRoomId, "logout", cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send logout before login for user {UserId}.", user.Id);
            }

            entity.State = TelegramConnectionState.Disconnected;
            entity.WebLoginUrl = null;
            entity.UpdatedAt = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // Validate existing management room is still accessible.
        // Only reset on definitive 404 (room gone). Transient errors (timeout, 5xx, 403)
        // keep the existing room to avoid breaking an in-progress login flow.
        if (!string.IsNullOrWhiteSpace(entity.ManagementRoomId))
        {
            try
            {
                var membership = await matrixApiClient.GetRoomMembershipAsync(
                    identity.AccessToken, entity.ManagementRoomId, bridgeOptions.Value.BotUserId, cancellationToken);
                if (string.IsNullOrWhiteSpace(membership))
                {
                    logger.LogInformation("Management room {RoomId} confirmed gone (404) for user {UserId}, will create new.", entity.ManagementRoomId, user.Id);
                    entity.ManagementRoomId = null;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                // Transient error — keep the existing room, log and continue
                logger.LogWarning(ex, "Could not validate management room {RoomId} for user {UserId} (transient error, keeping existing room).", entity.ManagementRoomId, user.Id);
            }
        }

        var managementRoomId = entity.ManagementRoomId;
        if (string.IsNullOrWhiteSpace(managementRoomId))
        {
            managementRoomId = await matrixApiClient.CreateDirectRoomAsync(
                identity.AccessToken, bridgeOptions.Value.BotUserId, cancellationToken);
        }

        entity.ManagementRoomId = managementRoomId;
        entity.WebLoginUrl = null;
        entity.UpdatedAt = timeProvider.GetUtcNow();

        var bridgeBotReady = await WaitForBridgeBotJoinAsync(identity.AccessToken, managementRoomId, cancellationToken);
        if (bridgeBotReady)
        {
            try
            {
                await matrixApiClient.SendTextMessageAsync(identity.AccessToken, managementRoomId, "login", cancellationToken);
                entity.State = TelegramConnectionState.LoginAwaitingPhone;
                logger.LogInformation("Started Telegram login for user {UserId} in room {RoomId}.", user.Id, managementRoomId);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                entity.State = TelegramConnectionState.Error;
                logger.LogWarning(ex, "Failed to send login command for user {UserId} in room {RoomId}.", user.Id, managementRoomId);
            }
        }
        else
        {
            entity.State = TelegramConnectionState.Error;
            logger.LogWarning("Bridge bot did not join room {RoomId} for user {UserId} before timeout.", managementRoomId, user.Id);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToDomain();
    }

    public async Task<TelegramConnection> ReconnectAsync(AppUser user, CancellationToken cancellationToken)
    {
        await DisconnectAsync(user.Id, cancellationToken);
        return await StartAsync(user, cancellationToken);
    }

    public async Task<TelegramConnection> StartChatLoginAsync(AppUser user, CancellationToken cancellationToken)
    {
        return await StartAsync(user, cancellationToken);
    }

    public async Task<TelegramConnection> SubmitLoginInputAsync(AppUser user, string input, CancellationToken cancellationToken)
    {
        if (pilotOptions.Value.DevSeedSampleData)
        {
            return await SubmitDevelopmentLoginInputAsync(user, input, cancellationToken);
        }

        var identity = await matrixProvisioningService.EnsureIdentityAsync(user, cancellationToken);
        if (!IsLiveAccessToken(identity.AccessToken))
        {
            return await SetStateAsync(user.Id, TelegramConnectionState.RequiresSetup, null, null, cancellationToken);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await FindOrCreateConnectionAsync(dbContext, user.Id, cancellationToken);

        // Auto-setup: ensure management room exists and bridge bot has joined
        if (string.IsNullOrWhiteSpace(entity.ManagementRoomId))
        {
            entity.ManagementRoomId = await matrixApiClient.CreateDirectRoomAsync(
                identity.AccessToken, bridgeOptions.Value.BotUserId, cancellationToken);
            entity.UpdatedAt = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!await WaitForBridgeBotJoinAsync(identity.AccessToken, entity.ManagementRoomId, cancellationToken))
        {
            entity.State = TelegramConnectionState.Error;
            entity.UpdatedAt = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Bridge bot did not join room {RoomId} for user {UserId} before timeout.", entity.ManagementRoomId, user.Id);
            return entity.ToDomain();
        }

        // For phone step: normalize input, re-issue login to ensure bridge is ready
        var isPhoneStep = entity.State is not (TelegramConnectionState.LoginAwaitingCode
            or TelegramConnectionState.LoginAwaitingPassword);

        var sanitizedInput = isPhoneStep ? NormalizePhoneNumber(input) : input;

        if (isPhoneStep && string.IsNullOrEmpty(sanitizedInput))
        {
            logger.LogWarning("Rejected invalid phone number for user {UserId}: input contained no digits.", user.Id);
            return entity.ToDomain();
        }

        // For phone step: send "login" first to ensure bridge is in login mode.
        // Not done for code/password — that would restart the flow.
        if (isPhoneStep)
        {
            try
            {
                await matrixApiClient.SendTextMessageAsync(identity.AccessToken, entity.ManagementRoomId, "login", cancellationToken);
                await Task.Delay(LoginRetryDelay, timeProvider, cancellationToken);
                entity.State = TelegramConnectionState.LoginAwaitingPhone;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send login command for user {UserId}. Proceeding anyway.", user.Id);
            }
        }

        try
        {
            await matrixApiClient.SendTextMessageAsync(identity.AccessToken, entity.ManagementRoomId, sanitizedInput, cancellationToken);
            logger.LogInformation("Sent login input for user {UserId} in room {RoomId}.", user.Id, entity.ManagementRoomId);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            entity.State = TelegramConnectionState.Error;
            entity.UpdatedAt = timeProvider.GetUtcNow();
            logger.LogWarning(ex, "Failed to send login input for user {UserId} in room {RoomId}.", user.Id, entity.ManagementRoomId);
        }

        entity.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
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
            IsLiveAccessToken(identity.AccessToken))
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
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await FindOrCreateConnectionAsync(dbContext, user.Id, cancellationToken);
        existing.State = TelegramConnectionState.LoginAwaitingPhone;
        existing.WebLoginUrl = null;
        existing.UpdatedAt = timeProvider.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);
        return existing.ToDomain();
    }

    private async Task<TelegramConnection> SubmitDevelopmentLoginInputAsync(
        AppUser user,
        string input,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await FindOrCreateConnectionAsync(dbContext, user.Id, cancellationToken);

        switch (existing.State)
        {
            case TelegramConnectionState.LoginAwaitingCode:
            case TelegramConnectionState.LoginAwaitingPassword:
            {
                if (string.IsNullOrWhiteSpace(input))
                {
                    return existing.ToDomain();
                }

                existing.State = TelegramConnectionState.Connected;
                existing.WebLoginUrl = null;
                existing.UpdatedAt = timeProvider.GetUtcNow();
                await dbContext.SaveChangesAsync(cancellationToken);
                return existing.ToDomain();
            }

            default:
            {
                var normalizedPhone = NormalizePhoneNumber(input);
                if (string.IsNullOrWhiteSpace(normalizedPhone))
                {
                    return existing.ToDomain();
                }

                existing.State = TelegramConnectionState.LoginAwaitingCode;
                existing.WebLoginUrl = null;
                existing.UpdatedAt = timeProvider.GetUtcNow();
                await dbContext.SaveChangesAsync(cancellationToken);
                return existing.ToDomain();
            }
        }
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
        if (identity is null || !IsLiveAccessToken(identity.AccessToken))
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
        var deadline = timeProvider.GetUtcNow().AddSeconds(bridgeOptions.Value.BridgeBotJoinTimeoutSeconds);
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

    private async Task<TelegramConnectionEntity> FindOrCreateConnectionAsync(
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
            UpdatedAt = timeProvider.GetUtcNow()
        };

        dbContext.TelegramConnections.Add(connection);
        return connection;
    }

    internal static bool IsLiveAccessToken(string? accessToken)
    {
        return !string.IsNullOrWhiteSpace(accessToken) &&
               !accessToken.StartsWith("dev-token-", StringComparison.Ordinal);
    }

    /// <summary>
    /// Strips formatting characters (spaces, dashes, parentheses) from a phone number,
    /// preserving only ASCII digits and an optional leading '+'.
    /// Returns empty string if no digits or '+' are found,
    /// allowing the caller to reject the input before sending to the bridge.
    /// </summary>
    internal static string NormalizePhoneNumber(string input)
    {
        var span = input.AsSpan().Trim();
        var buffer = new char[span.Length];
        var pos = 0;

        for (var i = 0; i < span.Length; i++)
        {
            if (char.IsAsciiDigit(span[i]) || (i == 0 && span[i] == '+'))
            {
                buffer[pos++] = span[i];
            }
        }

        return pos > 0 ? new string(buffer, 0, pos) : string.Empty;
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
