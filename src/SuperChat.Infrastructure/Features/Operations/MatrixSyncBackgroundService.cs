using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Persistence;
using SuperChat.Infrastructure.Services;

namespace SuperChat.Infrastructure.HostedServices;

public sealed class MatrixSyncBackgroundService(
    ITelegramConnectionService telegramConnectionService,
    IMessageNormalizationService normalizationService,
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    MatrixApiClient matrixApiClient,
    IOptions<PilotOptions> pilotOptions,
    IOptions<TelegramBridgeOptions> bridgeOptions,
    TimeProvider timeProvider,
    ILogger<MatrixSyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(4));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                if (pilotOptions.Value.DevSeedSampleData)
                {
                    await RunDevelopmentSeedAsync(stoppingToken);
                    continue;
                }

                await RunRealSyncAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Matrix sync tick failed.");
            }
        }
    }

    private async Task RunDevelopmentSeedAsync(CancellationToken cancellationToken)
    {
        var connections = await telegramConnectionService.GetReadyForDevelopmentSyncAsync(cancellationToken);
        foreach (var connection in connections)
        {
            var seeded = await SeedSampleMessagesAsync(connection.UserId, cancellationToken);
            await telegramConnectionService.MarkSynchronizedAsync(connection.UserId, timeProvider.GetUtcNow(), cancellationToken);

            if (seeded > 0)
            {
                logger.LogInformation("Seeded {SeededCount} development messages for user {UserId}.", seeded, connection.UserId);
            }
        }
    }

    private async Task RunRealSyncAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var syncTargets = await (
            from connection in dbContext.TelegramConnections
            join identity in dbContext.MatrixIdentities on connection.UserId equals identity.UserId
            join checkpoint in dbContext.SyncCheckpoints on connection.UserId equals checkpoint.UserId into checkpointGroup
            from checkpoint in checkpointGroup.DefaultIfEmpty()
            where connection.State != TelegramConnectionState.NotStarted &&
                  connection.State != TelegramConnectionState.Disconnected &&
                  identity.AccessToken != "" &&
                  !EF.Functions.Like(identity.AccessToken, "dev-token-%")
            select new MatrixSyncTarget(
                connection.UserId,
                connection.ManagementRoomId,
                connection.State,
                identity.MatrixUserId,
                identity.AccessToken,
                checkpoint == null ? null : checkpoint.NextBatchToken))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (var target in syncTargets)
        {
            await SyncUserAsync(target, cancellationToken);
        }
    }

    private async Task SyncUserAsync(MatrixSyncTarget target, CancellationToken cancellationToken)
    {
        MatrixSyncResult result;
        try
        {
            result = await matrixApiClient.SyncAsync(target.AccessToken, target.NextBatchToken, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Matrix /sync failed for user {UserId}.", target.UserId);
            await UpdateConnectionStateAsync(target.UserId, TelegramConnectionState.Error, cancellationToken);
            return;
        }

        var connected = target.State == TelegramConnectionState.Connected;
        string? discoveredLoginUrl = null;
        var ingestedMessages = 0;

        foreach (var room in result.Rooms)
        {
            foreach (var timelineEvent in room.Events)
            {
                if (room.RoomId == target.ManagementRoomId &&
                    string.Equals(timelineEvent.Sender, bridgeOptions.Value.BotUserId, StringComparison.Ordinal))
                {
                    discoveredLoginUrl ??= matrixApiClient.TryExtractFirstUrl(timelineEvent.Body)?.ToString();
                    connected = connected || LooksLikeSuccessfulLogin(timelineEvent.Body);
                    continue;
                }

                var senderName = DeriveSenderName(timelineEvent.Sender, target.MatrixUserId);
                var stored = await normalizationService.TryStoreAsync(
                    target.UserId,
                    room.RoomId,
                    timelineEvent.EventId,
                    senderName,
                    timelineEvent.Body,
                    timelineEvent.SentAt,
                    cancellationToken);

                if (stored)
                {
                    ingestedMessages++;
                    connected = true;
                }
            }
        }

        await PersistSyncStateAsync(
            target,
            result.NextBatchToken,
            discoveredLoginUrl,
            connected,
            ingestedMessages > 0,
            cancellationToken);
    }

    private async Task PersistSyncStateAsync(
        MatrixSyncTarget target,
        string? nextBatchToken,
        string? discoveredLoginUrl,
        bool connected,
        bool sawMessages,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var connection = await dbContext.TelegramConnections.SingleAsync(item => item.UserId == target.UserId, cancellationToken);
        var checkpoint = await dbContext.SyncCheckpoints.SingleOrDefaultAsync(item => item.UserId == target.UserId, cancellationToken);
        if (checkpoint is null)
        {
            checkpoint = new SyncCheckpointEntity
            {
                UserId = target.UserId
            };

            dbContext.SyncCheckpoints.Add(checkpoint);
        }

        checkpoint.NextBatchToken = nextBatchToken;
        checkpoint.UpdatedAt = timeProvider.GetUtcNow();

        if (!string.IsNullOrWhiteSpace(discoveredLoginUrl))
        {
            connection.WebLoginUrl = discoveredLoginUrl;
        }

        connection.UpdatedAt = timeProvider.GetUtcNow();
        if (connected)
        {
            connection.State = TelegramConnectionState.Connected;
            connection.WebLoginUrl = null;
        }
        else if (connection.State == TelegramConnectionState.NotStarted)
        {
            connection.State = TelegramConnectionState.BridgePending;
        }

        if (sawMessages)
        {
            connection.LastSyncedAt = timeProvider.GetUtcNow();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateConnectionStateAsync(
        Guid userId,
        TelegramConnectionState state,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var connection = await dbContext.TelegramConnections.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (connection is null)
        {
            return;
        }

        connection.State = state;
        connection.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> SeedSampleMessagesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var count = 0;

        count += await normalizationService.TryStoreAsync(
            userId,
            "!sales:matrix.localhost",
            "$evt-1",
            "Надя",
            "Пожалуйста, отправь обновлённое предложение завтра утром.",
            now.AddMinutes(-14),
            cancellationToken)
            ? 1
            : 0;

        count += await normalizationService.TryStoreAsync(
            userId,
            "!ops:matrix.localhost",
            "$evt-2",
            "Виктор",
            "У нас встреча с пилотной когортой в пятницу в 11:00.",
            now.AddMinutes(-9),
            cancellationToken)
            ? 1
            : 0;

        count += await normalizationService.TryStoreAsync(
            userId,
            "!founders:matrix.localhost",
            "$evt-3",
            "Ты",
            "Я отправлю правки по договору к концу дня.",
            now.AddMinutes(-7),
            cancellationToken)
            ? 1
            : 0;

        count += await normalizationService.TryStoreAsync(
            userId,
            "!replies:matrix.localhost",
            "$evt-4",
            "Алекс",
            "Все еще ждем ответ от Марины по hiring-плану.",
            now.AddMinutes(-3),
            cancellationToken)
            ? 1
            : 0;

        return count;
    }

    private static bool LooksLikeSuccessfulLogin(string message)
    {
        var normalized = message.ToLowerInvariant();
        return normalized.Contains("logged in", StringComparison.Ordinal) ||
               normalized.Contains("login successful", StringComparison.Ordinal) ||
               normalized.Contains("successfully logged in", StringComparison.Ordinal);
    }

    private static string DeriveSenderName(string senderId, string ownMatrixUserId)
    {
        if (string.Equals(senderId, ownMatrixUserId, StringComparison.Ordinal))
        {
            return "You";
        }

        if (string.IsNullOrWhiteSpace(senderId) || !senderId.StartsWith("@", StringComparison.Ordinal))
        {
            return "Unknown";
        }

        var colonIndex = senderId.IndexOf(':');
        var localpart = colonIndex > 1 ? senderId[1..colonIndex] : senderId[1..];
        if (localpart.StartsWith("telegram_", StringComparison.OrdinalIgnoreCase))
        {
            localpart = localpart["telegram_".Length..];
        }

        return localpart.Replace('-', ' ');
    }

    private sealed record MatrixSyncTarget(
        Guid UserId,
        string? ManagementRoomId,
        TelegramConnectionState State,
        string MatrixUserId,
        string AccessToken,
        string? NextBatchToken);
}
