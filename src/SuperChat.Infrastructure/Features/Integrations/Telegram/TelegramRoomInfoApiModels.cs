using System.Text.Json.Serialization;

namespace SuperChat.Infrastructure.Services;

internal sealed record TelegramRoomInfoApiResponse(
    [property: JsonPropertyName("peer_type")] string? PeerType,
    [property: JsonPropertyName("participant_count")] int? ParticipantCount,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("is_broadcast_channel")] bool IsBroadcastChannel);

internal sealed record TelegramSenderInfoApiResponse(
    [property: JsonPropertyName("telegram_user_id")] long? TelegramUserId,
    [property: JsonPropertyName("is_bot")] bool IsBot);
