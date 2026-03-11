using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;

namespace SuperChat.Infrastructure.Services;

public sealed partial class MatrixApiClient(
    HttpClient httpClient,
    IOptions<MatrixOptions> matrixOptions,
    ILogger<MatrixApiClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task EnsureUserAsync(string matrixUserId, CancellationToken cancellationToken)
    {
        var password = $"superchat-{Guid.NewGuid():N}";
        var request = CreateAdminRequest(
            HttpMethod.Put,
            $"/_synapse/admin/v2/users/{Uri.EscapeDataString(matrixUserId)}");

        request.Content = JsonContent.Create(new
        {
            password,
            admin = false,
            deactivated = false,
            logout_devices = false
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string> LoginAsUserAsync(string matrixUserId, CancellationToken cancellationToken)
    {
        using var request = CreateAdminRequest(
            HttpMethod.Post,
            $"/_synapse/admin/v1/users/{Uri.EscapeDataString(matrixUserId)}/login");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AdminLoginResponse>(JsonOptions, cancellationToken);
        if (string.IsNullOrWhiteSpace(payload?.AccessToken))
        {
            throw new InvalidOperationException($"Synapse did not return an access token for {matrixUserId}.");
        }

        return payload.AccessToken;
    }

    public async Task<string> CreateDirectRoomAsync(
        string accessToken,
        string inviteUserId,
        CancellationToken cancellationToken)
    {
        using var request = CreateUserRequest(HttpMethod.Post, "/_matrix/client/v3/createRoom", accessToken);
        request.Content = JsonContent.Create(new
        {
            invite = new[] { inviteUserId },
            is_direct = true,
            preset = "private_chat",
            name = "Telegram Bridge"
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CreateRoomResponse>(JsonOptions, cancellationToken);
        if (string.IsNullOrWhiteSpace(payload?.RoomId))
        {
            throw new InvalidOperationException("Matrix createRoom response did not include room_id.");
        }

        return payload.RoomId;
    }

    public async Task SendTextMessageAsync(
        string accessToken,
        string roomId,
        string body,
        CancellationToken cancellationToken)
    {
        var txnId = Guid.NewGuid().ToString("N");
        using var request = CreateUserRequest(
            HttpMethod.Put,
            $"/_matrix/client/v3/rooms/{Uri.EscapeDataString(roomId)}/send/m.room.message/{txnId}",
            accessToken);

        request.Content = JsonContent.Create(new
        {
            msgtype = "m.text",
            body
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<MatrixSyncResult> SyncAsync(
        string accessToken,
        string? since,
        CancellationToken cancellationToken)
    {
        var path = "/_matrix/client/v3/sync?timeout=1000&set_presence=offline";
        if (!string.IsNullOrWhiteSpace(since))
        {
            path += $"&since={Uri.EscapeDataString(since)}";
        }

        using var request = CreateUserRequest(HttpMethod.Get, path, accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<SyncResponse>(JsonOptions, cancellationToken)
            ?? new SyncResponse();

        var rooms = new List<MatrixTimelineRoom>();
        foreach (var room in payload.Rooms?.Join ?? new Dictionary<string, JoinedRoom>())
        {
            var events = new List<MatrixTimelineEvent>();
            foreach (var timelineEvent in room.Value.Timeline?.Events ?? [])
            {
                if (!string.Equals(timelineEvent.Type, "m.room.message", StringComparison.Ordinal))
                {
                    continue;
                }

                var body = TryGetString(timelineEvent.Content, "body");
                if (string.IsNullOrWhiteSpace(body))
                {
                    continue;
                }

                var msgType = TryGetString(timelineEvent.Content, "msgtype") ?? "m.text";
                events.Add(new MatrixTimelineEvent(
                    timelineEvent.EventId ?? string.Empty,
                    timelineEvent.Sender ?? string.Empty,
                    msgType,
                    body,
                    ParseTimestamp(timelineEvent.OriginServerTs)));
            }

            if (events.Count > 0)
            {
                rooms.Add(new MatrixTimelineRoom(room.Key, events));
            }
        }

        return new MatrixSyncResult(payload.NextBatch, rooms);
    }

    public Uri? TryExtractFirstUrl(string text)
    {
        var match = UrlRegex().Match(text);
        if (!match.Success)
        {
            return null;
        }

        var value = match.Value.TrimEnd('.', ',', ')', ']');
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return uri;
        }

        logger.LogDebug("Failed to parse URL from bridge message: {Message}", text);
        return null;
    }

    private HttpRequestMessage CreateAdminRequest(HttpMethod method, string path)
    {
        var token = matrixOptions.Value.AdminAccessToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Matrix admin access token is not configured.");
        }

        return CreateRequest(method, path, token);
    }

    private HttpRequestMessage CreateUserRequest(HttpMethod method, string path, string accessToken)
    {
        return CreateRequest(method, path, accessToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, string accessToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static DateTimeOffset ParseTimestamp(long? originServerTs)
    {
        return originServerTs is > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(originServerTs.Value)
            : DateTimeOffset.UtcNow;
    }

    private static string? TryGetString(JsonElement? content, string propertyName)
    {
        if (content is not { ValueKind: JsonValueKind.Object } objectContent ||
            !objectContent.TryGetProperty(propertyName, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.GetString();
    }

    [GeneratedRegex(@"https?://\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex UrlRegex();

    private sealed record AdminLoginResponse([property: JsonPropertyName("access_token")] string? AccessToken);

    private sealed record CreateRoomResponse([property: JsonPropertyName("room_id")] string? RoomId);

    private sealed class SyncResponse
    {
        [JsonPropertyName("next_batch")]
        public string? NextBatch { get; init; }

        public SyncRooms? Rooms { get; init; }
    }

    private sealed class SyncRooms
    {
        public Dictionary<string, JoinedRoom>? Join { get; init; }
    }

    private sealed class JoinedRoom
    {
        public TimelinePayload? Timeline { get; init; }
    }

    private sealed class TimelinePayload
    {
        public List<TimelineEventPayload>? Events { get; init; }
    }

    private sealed class TimelineEventPayload
    {
        [JsonPropertyName("event_id")]
        public string? EventId { get; init; }

        public string? Type { get; init; }

        public string? Sender { get; init; }

        [JsonPropertyName("origin_server_ts")]
        public long? OriginServerTs { get; init; }

        public JsonElement? Content { get; init; }
    }
}

public sealed record MatrixSyncResult(
    string? NextBatchToken,
    IReadOnlyList<MatrixTimelineRoom> Rooms);

public sealed record MatrixTimelineRoom(
    string RoomId,
    IReadOnlyList<MatrixTimelineEvent> Events);

public sealed record MatrixTimelineEvent(
    string EventId,
    string Sender,
    string MessageType,
    string Body,
    DateTimeOffset SentAt);
