using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

public sealed class TelegramRoomInfoService(
    HttpClient httpClient,
    ILogger<TelegramRoomInfoService> logger) : ITelegramRoomInfoService
{
    public async Task<TelegramRoomInfo?> GetRoomInfoAsync(
        string matrixUserId,
        string roomId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(matrixUserId) || string.IsNullOrWhiteSpace(roomId))
        {
            return null;
        }

        var requestUri = $"/rooms/{Uri.EscapeDataString(roomId)}/info?matrixUserId={Uri.EscapeDataString(matrixUserId)}";
        using var response = await httpClient.GetAsync(requestUri, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TelegramRoomInfoResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Telegram room helper returned an empty payload.");

        if (string.IsNullOrWhiteSpace(payload.PeerType))
        {
            logger.LogWarning("Telegram room helper returned empty peer type for room {RoomId}.", roomId);
            return null;
        }

        return new TelegramRoomInfo(
            roomId,
            payload.PeerType,
            payload.ParticipantCount,
            payload.Title,
            payload.IsBroadcastChannel);
    }

    public async Task<TelegramSenderInfo?> GetSenderInfoAsync(
        string matrixUserId,
        string senderMatrixUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(matrixUserId) || string.IsNullOrWhiteSpace(senderMatrixUserId))
        {
            return null;
        }

        var requestUri = $"/senders/{Uri.EscapeDataString(senderMatrixUserId)}/info?matrixUserId={Uri.EscapeDataString(matrixUserId)}";
        using var response = await httpClient.GetAsync(requestUri, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TelegramSenderInfoResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Telegram sender helper returned an empty payload.");

        if (payload.TelegramUserId is null)
        {
            logger.LogWarning("Telegram sender helper returned empty Telegram user id for sender {SenderMatrixUserId}.", senderMatrixUserId);
            return null;
        }

        return new TelegramSenderInfo(
            senderMatrixUserId,
            payload.TelegramUserId.Value,
            payload.IsBot);
    }

    private sealed record TelegramRoomInfoResponse(
        [property: JsonPropertyName("peer_type")] string? PeerType,
        [property: JsonPropertyName("participant_count")] int? ParticipantCount,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("is_broadcast_channel")] bool IsBroadcastChannel);

    private sealed record TelegramSenderInfoResponse(
        [property: JsonPropertyName("telegram_user_id")] long? TelegramUserId,
        [property: JsonPropertyName("is_bot")] bool IsBot);
}
