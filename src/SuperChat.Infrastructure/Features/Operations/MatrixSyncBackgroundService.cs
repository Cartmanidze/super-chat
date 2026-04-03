using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Contracts.Features.Integrations;
using SuperChat.Contracts.Features.Integrations.Telegram;
using SuperChat.Contracts.Features.Messaging;
using SuperChat.Domain.Features.Integrations;
using SuperChat.Domain.Features.Integrations.Telegram;
using SuperChat.Infrastructure.Diagnostics;
using SuperChat.Infrastructure.Features.Integrations.Matrix;
using SuperChat.Infrastructure.Features.Operations.Sync;
using SuperChat.Infrastructure.Shared.Persistence;
using System.Diagnostics;

namespace SuperChat.Infrastructure.Features.Operations;

public sealed class MatrixSyncBackgroundService(
    IIntegrationConnectionService integrationConnectionService,
    ITelegramRoomInfoService telegramRoomInfoService,
    ChatRoomHandler chatRoomHandler,
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    MatrixApiClient matrixApiClient,
    IOptions<PilotOptions> pilotOptions,
    IOptions<TelegramBridgeOptions> bridgeOptions,
    IMessageNormalizationService normalizationService,
    TimeProvider timeProvider,
    ILogger<MatrixSyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(4));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            var mode = pilotOptions.Value.DevSeedSampleData ? "development" : "production";
            var result = "succeeded";
            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (pilotOptions.Value.DevSeedSampleData)
                {
                    var seedStats = await RunDevelopmentSeedAsync(stoppingToken);
                    if (seedStats.MessagesIngested > 0)
                    {
                        SuperChatMetrics.MatrixSyncMessagesIngestedTotal.WithLabels(mode).Inc(seedStats.MessagesIngested);
                    }

                    SuperChatMetrics.MatrixSyncTicksTotal.WithLabels(mode, result).Inc();
                    SuperChatMetrics.MatrixSyncTickDurationSeconds.WithLabels(mode, result).Observe(stopwatch.Elapsed.TotalSeconds);
                    continue;
                }

                var syncStats = await RunRealSyncAsync(stoppingToken);
                if (syncStats.MessagesIngested > 0)
                {
                    SuperChatMetrics.MatrixSyncMessagesIngestedTotal.WithLabels(mode).Inc(syncStats.MessagesIngested);
                }

                SuperChatMetrics.MatrixSyncTicksTotal.WithLabels(mode, result).Inc();
                SuperChatMetrics.MatrixSyncTickDurationSeconds.WithLabels(mode, result).Observe(stopwatch.Elapsed.TotalSeconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                result = "failed";
                SuperChatMetrics.MatrixSyncTicksTotal.WithLabels(mode, result).Inc();
                SuperChatMetrics.MatrixSyncTickDurationSeconds.WithLabels(mode, result).Observe(stopwatch.Elapsed.TotalSeconds);
                logger.LogWarning(ex, "Matrix sync tick failed.");
            }
        }
    }

    private async Task<MatrixSyncTickStats> RunDevelopmentSeedAsync(CancellationToken cancellationToken)
    {
        var connections = await integrationConnectionService.GetReadyForDevelopmentSyncAsync(
            IntegrationProvider.Telegram,
            cancellationToken);
        var seededMessages = 0;

        foreach (var connection in connections)
        {
            var seeded = await SeedSampleMessagesAsync(connection.UserId, cancellationToken);
            seededMessages += seeded;
            await integrationConnectionService.MarkSynchronizedAsync(
                connection.UserId,
                IntegrationProvider.Telegram,
                timeProvider.GetUtcNow(),
                cancellationToken);

            if (seeded > 0)
            {
                logger.LogInformation("Seeded {SeededCount} development messages for user {UserId}.", seeded, connection.UserId);
            }
        }

        return new MatrixSyncTickStats(connections.Count, 0, seededMessages, 0);
    }

    private async Task<MatrixSyncTickStats> RunRealSyncAsync(CancellationToken cancellationToken)
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

        var roomsObserved = 0;
        var messagesIngested = 0;
        var invitedRoomsJoined = 0;

        foreach (var target in syncTargets)
        {
            var userResult = await SyncUserAsync(target, cancellationToken);
            roomsObserved += userResult.RoomsObserved;
            messagesIngested += userResult.MessagesIngested;
            invitedRoomsJoined += userResult.InvitedRoomsJoined;
        }

        return new MatrixSyncTickStats(syncTargets.Count, roomsObserved, messagesIngested, invitedRoomsJoined);
    }

    private async Task<MatrixSyncUserResult> SyncUserAsync(MatrixSyncTarget target, CancellationToken cancellationToken)
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
            return new MatrixSyncUserResult(0, 0, 0);
        }

        var connected = target.State == TelegramConnectionState.Connected;
        var ingestedMessages = 0;
        var joinedInvitedRooms = 0;
        var invitedRoomsToJoin = SyncStateResolver.GetInvitedRoomsToJoin(result.InvitedRoomIds, target.ManagementRoomId);
        var senderInfoBySenderId = new Dictionary<string, TelegramSenderInfo?>(StringComparer.Ordinal);
        ManagementRoomResult? managementResult = null;

        foreach (var roomId in invitedRoomsToJoin)
        {
            try
            {
                await matrixApiClient.JoinRoomAsync(target.AccessToken, roomId, cancellationToken);
                joinedInvitedRooms++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to auto-join invited Matrix room {RoomId} for user {UserId}.", roomId, target.UserId);
            }
        }

        foreach (var room in result.Rooms)
        {
            var isManagementRoom = SyncStateResolver.IsManagementRoom(room.RoomId, target.ManagementRoomId);

            if (isManagementRoom)
            {
                managementResult = ManagementRoomHandler.Process(
                    room.Events,
                    bridgeOptions.Value.BotUserId,
                    matrixApiClient.TryExtractFirstUrl);

                if (managementResult.Connected)
                {
                    connected = true;
                }
                else if (managementResult.LostConnection)
                {
                    connected = false;
                }

                if (ManagementRoomHandler.ShouldRetryBridgeLogin(
                        target.State, connected, managementResult.DiscoveredLoginUrl, managementResult.SawBridgeGreeting))
                {
                    try
                    {
                        await matrixApiClient.SendTextMessageAsync(target.AccessToken, room.RoomId, "login", cancellationToken);
                        logger.LogInformation(
                            "Re-issued Telegram bridge login for user {UserId} after bridge greeting in room {RoomId}.",
                            target.UserId,
                            room.RoomId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(
                            ex,
                            "Failed to re-issue Telegram bridge login for user {UserId} in room {RoomId}.",
                            target.UserId,
                            room.RoomId);
                    }
                }

                continue;
            }

            TelegramRoomInfo? telegramRoomInfo = null;
            try
            {
                telegramRoomInfo = await telegramRoomInfoService.GetRoomInfoAsync(
                    target.MatrixUserId,
                    room.RoomId,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to resolve Telegram room info for room {RoomId} and user {UserId}. Skipping room ingestion.",
                    room.RoomId,
                    target.UserId);
            }

            if (!ChatRoomHandler.ShouldIngestRoom(
                    room.RoomId,
                    target.ManagementRoomId,
                    room.IsDirect,
                    telegramRoomInfo,
                    room.MemberCount,
                    pilotOptions.Value.EnableGroupIngestion,
                    pilotOptions.Value.MaxIngestedGroupMembers))
            {
                continue;
            }

            var chatResult = await chatRoomHandler.ProcessRoomEventsAsync(
                room,
                target.UserId,
                target.MatrixUserId,
                senderInfoBySenderId,
                cancellationToken);

            ingestedMessages += chatResult.IngestedMessages;
            if (chatResult.Connected)
            {
                connected = true;
            }
        }

        await PersistSyncStateAsync(
            target,
            result.NextBatchToken,
            managementResult,
            connected,
            ingestedMessages > 0,
            cancellationToken);

        return new MatrixSyncUserResult(result.Rooms.Count, ingestedMessages, joinedInvitedRooms);
    }

    private async Task PersistSyncStateAsync(
        MatrixSyncTarget target,
        string? nextBatchToken,
        ManagementRoomResult? managementResult,
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

        var discoveredLoginUrl = managementResult?.DiscoveredLoginUrl;
        if (!string.IsNullOrWhiteSpace(discoveredLoginUrl))
        {
            connection.WebLoginUrl = discoveredLoginUrl;
        }

        connection.UpdatedAt = timeProvider.GetUtcNow();
        connection.State = SyncStateResolver.ResolveConnectionStateAfterSuccessfulSync(
            connection.State,
            connected,
            managementResult?.LostConnection ?? false,
            managementResult?.DetectedLoginStep);
        if (connection.State == TelegramConnectionState.Connected)
        {
            connection.WebLoginUrl = null;
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

    private sealed record MatrixSyncTarget(
        Guid UserId,
        string? ManagementRoomId,
        TelegramConnectionState State,
        string MatrixUserId,
        string AccessToken,
        string? NextBatchToken);

    private sealed record MatrixSyncTickStats(
        int UsersProcessed,
        int RoomsObserved,
        int MessagesIngested,
        int InvitedRoomsJoined);

    private sealed record MatrixSyncUserResult(
        int RoomsObserved,
        int MessagesIngested,
        int InvitedRoomsJoined);
}
