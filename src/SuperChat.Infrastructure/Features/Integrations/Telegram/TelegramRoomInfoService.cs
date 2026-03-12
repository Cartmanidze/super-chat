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

        return new TelegramRoomInfo(roomId, payload.PeerType, payload.ParticipantCount, payload.Title);
    }

    private sealed record TelegramRoomInfoResponse(
        [property: JsonPropertyName("peer_type")] string? PeerType,
        [property: JsonPropertyName("participant_count")] int? ParticipantCount,
        [property: JsonPropertyName("title")] string? Title);
}
