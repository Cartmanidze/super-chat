using Microsoft.Extensions.Logging;
using SuperChat.Contracts.Features.Integrations.Telegram;
using SuperChat.Contracts.Features.Messaging;
using SuperChat.Infrastructure.Diagnostics;
using SuperChat.Infrastructure.Features.Integrations.Matrix;
using SuperChat.Infrastructure.Features.Messaging;

namespace SuperChat.Infrastructure.Features.Operations.Sync;

public sealed class ChatRoomHandler(
    ITelegramRoomInfoService telegramRoomInfoService,
    IncomingMessageFilter incomingMessageFilter,
    IMessageNormalizationService normalizationService,
    ILogger<ChatRoomHandler> logger)
{
    internal static bool ShouldIngestRoom(
        string roomId,
        string? managementRoomId,
        bool isDirect,
        TelegramRoomInfo? roomInfo,
        int? matrixMemberCount,
        bool enableGroupIngestion,
        int maxIngestedGroupMembers)
    {
        if (SyncStateResolver.IsManagementRoom(roomId, managementRoomId))
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
            if (!enableGroupIngestion)
            {
                return false;
            }

            return matrixMemberCount is int inferredCount &&
                   inferredCount <= maxIngestedGroupMembers;
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
    }

    internal static string DeriveSenderName(string senderId, string ownMatrixUserId)
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

    public async Task<ChatRoomProcessingResult> ProcessRoomEventsAsync(
        MatrixTimelineRoom room,
        Guid userId,
        string matrixUserId,
        IDictionary<string, TelegramSenderInfo?> senderInfoCache,
        CancellationToken cancellationToken)
    {
        var ingestedMessages = 0;
        var connected = false;

        foreach (var timelineEvent in room.Events)
        {
            var senderInfo = await ResolveSenderInfoAsync(
                matrixUserId,
                userId,
                timelineEvent.Sender,
                senderInfoCache,
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
                    userId,
                    ingestionDecision.Reason);
                continue;
            }

            var senderName = DeriveSenderName(timelineEvent.Sender, matrixUserId);
            logger.LogInformation(
                "Accepted Matrix event for normalization. EventId={EventId}, SenderName={SenderName}, SentAt={SentAt}, BodyLength={BodyLength}, Preview={Preview}.",
                timelineEvent.EventId,
                senderName,
                timelineEvent.SentAt,
                timelineEvent.Body.Length,
                MessagePipelineTrace.CreatePreview(timelineEvent.Body));
            var stored = await normalizationService.TryStoreAsync(
                userId,
                room.RoomId,
                timelineEvent.EventId,
                senderName,
                timelineEvent.Body,
                timelineEvent.SentAt,
                cancellationToken);

            if (stored)
            {
                logger.LogInformation(
                    "Matrix event entered message pipeline successfully. EventId={EventId}, SenderName={SenderName}, SentAt={SentAt}.",
                    timelineEvent.EventId,
                    senderName,
                    timelineEvent.SentAt);
                ingestedMessages++;
                connected = true;
            }
        }

        return new ChatRoomProcessingResult(ingestedMessages, connected);
    }

    private async Task<TelegramSenderInfo?> ResolveSenderInfoAsync(
        string matrixUserId,
        Guid userId,
        string senderId,
        IDictionary<string, TelegramSenderInfo?> senderInfoCache,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(senderId) ||
            string.Equals(senderId, matrixUserId, StringComparison.Ordinal))
        {
            return null;
        }

        if (senderInfoCache.TryGetValue(senderId, out var cachedSenderInfo))
        {
            return cachedSenderInfo;
        }

        try
        {
            var senderInfo = await telegramRoomInfoService.GetSenderInfoAsync(
                matrixUserId,
                senderId,
                cancellationToken);
            senderInfoCache[senderId] = senderInfo;
            return senderInfo;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to resolve Telegram sender info for sender {SenderId} and user {UserId}. Continuing without sender metadata.",
                senderId,
                userId);
            senderInfoCache[senderId] = null;
            return null;
        }
    }
}

public sealed record ChatRoomProcessingResult(int IngestedMessages, bool Connected);
