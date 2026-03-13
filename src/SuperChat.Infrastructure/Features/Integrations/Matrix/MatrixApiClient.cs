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

    public async Task JoinRoomAsync(
        string accessToken,
        string roomId,
        CancellationToken cancellationToken)
    {
        using var request = CreateUserRequest(
            HttpMethod.Post,
            $"/_matrix/client/v3/rooms/{Uri.EscapeDataString(roomId)}/join",
            accessToken);
        request.Content = JsonContent.Create(new { });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
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
        var invitedRoomIds = new List<string>();
        var directRoomIds = GetDirectRoomIds(payload.AccountData);

        foreach (var room in payload.Rooms?.Invite ?? new Dictionary<string, InvitedRoom>())
        {
            if (!string.IsNullOrWhiteSpace(room.Key))
            {
                invitedRoomIds.Add(room.Key);
            }
        }

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
                rooms.Add(new MatrixTimelineRoom(
                    room.Key,
                    events,
                    GetMemberCount(room.Value.Summary),
                    directRoomIds.Contains(room.Key)));
            }
        }

        return new MatrixSyncResult(payload.NextBatch, rooms, invitedRoomIds);
    }

    public async Task<int> GetJoinedMemberCountAsync(
        string accessToken,
        string roomId,
        CancellationToken cancellationToken)
    {
        using var request = CreateUserRequest(
            HttpMethod.Get,
            $"/_matrix/client/v3/rooms/{Uri.EscapeDataString(roomId)}/joined_members",
            accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JoinedMembersResponse>(JsonOptions, cancellationToken)
            ?? new JoinedMembersResponse();

        return payload.Joined?.Count ?? 0;
    }

    public async Task<string?> GetRoomDisplayNameAsync(
        string accessToken,
        string roomId,
        string? currentMatrixUserId,
        CancellationToken cancellationToken)
    {
        using var request = CreateUserRequest(
            HttpMethod.Get,
            $"/_matrix/client/v3/rooms/{Uri.EscapeDataString(roomId)}/state",
            accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<RoomStateEventPayload>>(JsonOptions, cancellationToken)
            ?? [];

        var roomName = payload
            .Where(item => string.Equals(item.Type, "m.room.name", StringComparison.Ordinal))
            .Select(item => TryGetString(item.Content, "name"))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (!string.IsNullOrWhiteSpace(roomName))
        {
            return roomName;
        }

        var canonicalAlias = payload
            .Where(item => string.Equals(item.Type, "m.room.canonical_alias", StringComparison.Ordinal))
            .Select(item => TryGetString(item.Content, "alias"))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (!string.IsNullOrWhiteSpace(canonicalAlias))
        {
            return canonicalAlias;
        }

        var memberDisplayNames = payload
            .Where(item => string.Equals(item.Type, "m.room.member", StringComparison.Ordinal))
            .Where(item => string.Equals(TryGetString(item.Content, "membership"), "join", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(TryGetString(item.Content, "membership"), "invite", StringComparison.OrdinalIgnoreCase))
            .Where(item => !string.Equals(item.StateKey, currentMatrixUserId, StringComparison.Ordinal))
            .Select(item => TryGetString(item.Content, "displayname"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Where(value => !string.Equals(value, "Telegram Bridge", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToList();

        return memberDisplayNames.Count switch
        {
            0 => null,
            1 => memberDisplayNames[0],
            _ => string.Join(", ", memberDisplayNames)
        };
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

    private static HashSet<string> GetDirectRoomIds(AccountDataPayload? accountData)
    {
        var roomIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var accountDataEvent in accountData?.Events ?? [])
        {
            if (!string.Equals(accountDataEvent.Type, "m.direct", StringComparison.Ordinal) ||
                accountDataEvent.Content is not { ValueKind: JsonValueKind.Object } directMappings)
            {
                continue;
            }

            foreach (var mapping in directMappings.EnumerateObject())
            {
                if (mapping.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var roomId in mapping.Value.EnumerateArray())
                {
                    var value = roomId.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        roomIds.Add(value);
                    }
                }
            }
        }

        return roomIds;
    }

    private static int? GetMemberCount(RoomSummaryPayload? summary)
    {
        if (summary?.JoinedMemberCount is null && summary?.InvitedMemberCount is null)
        {
            return null;
        }

        return Math.Max(0, summary?.JoinedMemberCount.GetValueOrDefault() ?? 0) +
               Math.Max(0, summary?.InvitedMemberCount.GetValueOrDefault() ?? 0);
    }

    [GeneratedRegex(@"https?://\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex UrlRegex();

    private sealed record AdminLoginResponse([property: JsonPropertyName("access_token")] string? AccessToken);

    private sealed record CreateRoomResponse([property: JsonPropertyName("room_id")] string? RoomId);

    private sealed class SyncResponse
    {
        [JsonPropertyName("next_batch")]
        public string? NextBatch { get; init; }

        [JsonPropertyName("account_data")]
        public AccountDataPayload? AccountData { get; init; }

        public SyncRooms? Rooms { get; init; }
    }

    private sealed class SyncRooms
    {
        public Dictionary<string, JoinedRoom>? Join { get; init; }

        public Dictionary<string, InvitedRoom>? Invite { get; init; }
    }

    private sealed class JoinedRoom
    {
        public RoomSummaryPayload? Summary { get; init; }

        public TimelinePayload? Timeline { get; init; }
    }

    private sealed class InvitedRoom;

    private sealed class AccountDataPayload
    {
        public List<AccountDataEventPayload>? Events { get; init; }
    }

    private sealed class AccountDataEventPayload
    {
        public string? Type { get; init; }

        public JsonElement? Content { get; init; }
    }

    private sealed class RoomSummaryPayload
    {
        [JsonPropertyName("m.joined_member_count")]
        public int? JoinedMemberCount { get; init; }

        [JsonPropertyName("m.invited_member_count")]
        public int? InvitedMemberCount { get; init; }
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

    private sealed class JoinedMembersResponse
    {
        public Dictionary<string, object>? Joined { get; init; }
    }

    private sealed class RoomStateEventPayload
    {
        public string? Type { get; init; }

        [JsonPropertyName("state_key")]
        public string? StateKey { get; init; }

        public JsonElement? Content { get; init; }
    }
}

public sealed record MatrixSyncResult(
    string? NextBatchToken,
    IReadOnlyList<MatrixTimelineRoom> Rooms,
    IReadOnlyList<string> InvitedRoomIds);

public sealed record MatrixTimelineRoom(
    string RoomId,
    IReadOnlyList<MatrixTimelineEvent> Events,
    int? MemberCount,
    bool IsDirect);

public sealed record MatrixTimelineEvent(
    string EventId,
    string Sender,
    string MessageType,
    string Body,
    DateTimeOffset SentAt);
