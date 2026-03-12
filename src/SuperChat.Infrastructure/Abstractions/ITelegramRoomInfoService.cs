namespace SuperChat.Infrastructure.Abstractions;

public interface ITelegramRoomInfoService
{
    Task<TelegramRoomInfo?> GetRoomInfoAsync(
        string matrixUserId,
        string roomId,
        CancellationToken cancellationToken);
}

public sealed record TelegramRoomInfo(
    string RoomId,
    string PeerType,
    int? ParticipantCount,
    string? Title,
    bool IsBroadcastChannel);
