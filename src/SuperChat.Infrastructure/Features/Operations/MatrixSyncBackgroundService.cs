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
    IIntegrationConnectionService integrationConnectionService,
    ITelegramRoomInfoService telegramRoomInfoService,
    IncomingMessageFilter incomingMessageFilter,
    IMessageNormalizationService normalizationService,
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    MatrixApiClient matrixApiClient,
    IOptions<PilotOptions> pilotOptions,
    IOptions<TelegramBridgeOptions> bridgeOptions,
    TimeProvider timeProvider,
    IWorkerRuntimeMonitor workerRuntimeMonitor,
    ILogger<MatrixSyncBackgroundService> logger) : BackgroundService
{
    private const string WorkerKey = "matrix-sync";
    private const string WorkerDisplayName = "Matrix Sync";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        workerRuntimeMonitor.RegisterWorker(WorkerKey, WorkerDisplayName);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(4));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                workerRuntimeMonitor.MarkRunning(
                    WorkerKey,
                    WorkerDisplayName,
                    pilotOptions.Value.DevSeedSampleData ? "Development seed mode." : "Production sync tick.");

                if (pilotOptions.Value.DevSeedSampleData)
                {
                    var seedStats = await RunDevelopmentSeedAsync(stoppingToken);
                    workerRuntimeMonitor.MarkSucceeded(
                        WorkerKey,
                        WorkerDisplayName,
                        $"Mode=Development, Users={seedStats.UsersProcessed}, Messages={seedStats.MessagesIngested}");
                    continue;
                }

                var syncStats = await RunRealSyncAsync(stoppingToken);
                workerRuntimeMonitor.MarkSucceeded(
                    WorkerKey,
                    WorkerDisplayName,
                    $"Mode=Production, Users={syncStats.UsersProcessed}, Rooms={syncStats.RoomsObserved}, Messages={syncStats.MessagesIngested}, Joined={syncStats.InvitedRoomsJoined}");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                workerRuntimeMonitor.MarkFailed(WorkerKey, WorkerDisplayName, ex);
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
        string? discoveredLoginUrl = null;
        var ingestedMessages = 0;
        var joinedInvitedRooms = 0;
        var invitedRoomsToJoin = GetInvitedRoomsToJoin(result.InvitedRoomIds, target.ManagementRoomId);
        var senderInfoBySenderId = new Dictionary<string, TelegramSenderInfo?>(StringComparer.Ordinal);

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
            var isManagementRoom = IsManagementRoom(room.RoomId, target.ManagementRoomId);
            TelegramRoomInfo? telegramRoomInfo = null;
            var sawBridgeGreeting = false;

            if (!isManagementRoom)
            {
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

                if (!ShouldIngestRoom(
                        room.RoomId,
                        target.ManagementRoomId,
                        room.IsDirect,
                        telegramRoomInfo,
                        pilotOptions.Value.EnableGroupIngestion,
                        pilotOptions.Value.MaxIngestedGroupMembers))
                {
                    continue;
                }
            }

            foreach (var timelineEvent in room.Events)
            {
                if (isManagementRoom)
                {
                    if (string.Equals(timelineEvent.Sender, bridgeOptions.Value.BotUserId, StringComparison.Ordinal))
                    {
                        discoveredLoginUrl ??= matrixApiClient.TryExtractFirstUrl(timelineEvent.Body)?.ToString();
                        connected = connected || LooksLikeSuccessfulLogin(timelineEvent.Body);
                        sawBridgeGreeting = sawBridgeGreeting || LooksLikeBridgeGreeting(timelineEvent.Body);
                    }

                    continue;
                }

                var senderInfo = await ResolveSenderInfoAsync(
                    target,
                    timelineEvent.Sender,
                    senderInfoBySenderId,
                    cancellationToken);

                var ingestionDecision = incomingMessageFilter.Evaluate(
                    timelineEvent.MessageType,
                    timelineEvent.Body,
                    senderInfo?.IsBot);
                if (!ingestionDecision.ShouldIngest)
                {
                    logger.LogDebug(
                        "Skipped Matrix event {EventId} in room {RoomId} for user {UserId}. Reason={Reason}.",
                        timelineEvent.EventId,
                        room.RoomId,
                        target.UserId,
                        ingestionDecision.Reason);
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

            if (isManagementRoom &&
                ShouldRetryBridgeLogin(target.State, connected, discoveredLoginUrl, sawBridgeGreeting))
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
        }

        await PersistSyncStateAsync(
            target,
            result.NextBatchToken,
            discoveredLoginUrl,
            connected,
            ingestedMessages > 0,
            cancellationToken);

        return new MatrixSyncUserResult(result.Rooms.Count, ingestedMessages, joinedInvitedRooms);
    }

    private async Task<TelegramSenderInfo?> ResolveSenderInfoAsync(
        MatrixSyncTarget target,
        string senderId,
        IDictionary<string, TelegramSenderInfo?> senderInfoBySenderId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(senderId) ||
            string.Equals(senderId, target.MatrixUserId, StringComparison.Ordinal))
        {
            return null;
        }

        if (senderInfoBySenderId.TryGetValue(senderId, out var cachedSenderInfo))
        {
            return cachedSenderInfo;
        }

        try
        {
            var senderInfo = await telegramRoomInfoService.GetSenderInfoAsync(
                target.MatrixUserId,
                senderId,
                cancellationToken);
            senderInfoBySenderId[senderId] = senderInfo;
            return senderInfo;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to resolve Telegram sender info for sender {SenderId} and user {UserId}. Continuing without sender metadata.",
                senderId,
                target.UserId);
            senderInfoBySenderId[senderId] = null;
            return null;
        }
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
        connection.State = ResolveConnectionStateAfterSuccessfulSync(connection.State, connected);
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

    private static bool LooksLikeSuccessfulLogin(string message)
    {
        var normalized = message.ToLowerInvariant();
        return normalized.Contains("logged in", StringComparison.Ordinal) ||
               normalized.Contains("login successful", StringComparison.Ordinal) ||
               normalized.Contains("successfully logged in", StringComparison.Ordinal);
    }

    internal static bool LooksLikeBridgeGreeting(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = message.ToLowerInvariant();
        return normalized.Contains("telegram bridge bot", StringComparison.Ordinal) ||
               normalized.Contains("telegram bridge", StringComparison.Ordinal) &&
               normalized.Contains("hello", StringComparison.Ordinal);
    }

    internal static bool ShouldRetryBridgeLogin(
        TelegramConnectionState currentState,
        bool connected,
        string? discoveredLoginUrl,
        bool sawBridgeGreeting)
    {
        return !connected &&
               string.IsNullOrWhiteSpace(discoveredLoginUrl) &&
               sawBridgeGreeting &&
               (currentState == TelegramConnectionState.BridgePending ||
                currentState == TelegramConnectionState.Error);
    }

    internal static TelegramConnectionState ResolveConnectionStateAfterSuccessfulSync(
        TelegramConnectionState currentState,
        bool connected)
    {
        if (connected)
        {
            return TelegramConnectionState.Connected;
        }

        return currentState switch
        {
            TelegramConnectionState.NotStarted => TelegramConnectionState.BridgePending,
            TelegramConnectionState.Error => TelegramConnectionState.BridgePending,
            _ => currentState
        };
    }

    internal static bool IsManagementRoom(string roomId, string? managementRoomId)
    {
        return string.Equals(roomId, managementRoomId, StringComparison.Ordinal);
    }

    internal static IReadOnlyList<string> GetInvitedRoomsToJoin(
        IReadOnlyList<string> invitedRoomIds,
        string? managementRoomId)
    {
        return invitedRoomIds
            .Where(roomId => !string.IsNullOrWhiteSpace(roomId))
            .Where(roomId => !IsManagementRoom(roomId, managementRoomId))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    internal static bool ShouldIngestRoom(
        string roomId,
        string? managementRoomId,
        bool isDirect,
        TelegramRoomInfo? roomInfo,
        bool enableGroupIngestion,
        int maxIngestedGroupMembers)
    {
        if (IsManagementRoom(roomId, managementRoomId))
        {
            return false;
        }

        if (roomInfo?.IsBroadcastChannel == true)
        {
            return false;
        }

        if (string.Equals(roomInfo?.PeerType, "user", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (isDirect)
        {
            return true;
        }

        if (roomInfo is null)
        {
            return false;
        }

        if (!enableGroupIngestion)
        {
            return false;
        }

        return roomInfo.ParticipantCount is int participantCount &&
               participantCount <= maxIngestedGroupMembers;
    }

    internal static bool ShouldIngestMessageBody(string body)
    {
        return IncomingMessageFilter.ShouldIngestMessageBody(body);

        /*

               !normalized.StartsWith("Переслано из канала ", StringComparison.OrdinalIgnoreCase);
        */
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
