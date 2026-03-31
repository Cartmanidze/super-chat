namespace SuperChat.Contracts.Features.Integrations.Telegram;

public interface ITelegramRoomInfoService
{
    Task<TelegramRoomInfo?> GetRoomInfoAsync(
        string matrixUserId,
        string roomId,
        CancellationToken cancellationToken);

    Task<TelegramSenderInfo?> GetSenderInfoAsync(
        string matrixUserId,
        string senderMatrixUserId,
        CancellationToken cancellationToken);
}

public sealed record TelegramRoomInfo(
    string RoomId,
    string PeerType,
    int? ParticipantCount,
    string? Title,
    bool IsBroadcastChannel);

public sealed record TelegramSenderInfo(
    string SenderMatrixUserId,
    long TelegramUserId,
    bool IsBot);
