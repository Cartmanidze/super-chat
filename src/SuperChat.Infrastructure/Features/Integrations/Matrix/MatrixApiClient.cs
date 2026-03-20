using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Integrations.Matrix;

namespace SuperChat.Infrastructure.Features.Integrations.Matrix;

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

        request.Content = JsonContent.Create(new MatrixEnsureUserRequest(
            password,
            false,
            false,
            false));

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

        var payload = await response.Content.ReadFromJsonAsync<MatrixAdminLoginResponse>(JsonOptions, cancellationToken);
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
        request.Content = JsonContent.Create(new MatrixCreateDirectRoomRequest(
            [inviteUserId],
            true,
            "private_chat",
            "Telegram Bridge"));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<MatrixCreateRoomResponse>(JsonOptions, cancellationToken);
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
        request.Content = JsonContent.Create(new MatrixEmptyRequest());

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string?> GetRoomMembershipAsync(
        string accessToken,
        string roomId,
        string matrixUserId,
        CancellationToken cancellationToken)
    {
        using var request = CreateUserRequest(
            HttpMethod.Get,
            $"/_matrix/client/v3/rooms/{Uri.EscapeDataString(roomId)}/state/m.room.member/{Uri.EscapeDataString(matrixUserId)}",
            accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<MatrixRoomMemberStatePayload>(JsonOptions, cancellationToken);
        return payload?.Membership;
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

        request.Content = JsonContent.Create(new MatrixSendTextMessageRequest("m.text", body));

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

        var payload = await response.Content.ReadFromJsonAsync<MatrixSyncResponse>(JsonOptions, cancellationToken)
            ?? new MatrixSyncResponse();

        var rooms = new List<MatrixTimelineRoom>();
        var invitedRoomIds = new List<string>();
        var directRoomIds = payload.AccountData.GetDirectRoomIds();

        foreach (var room in payload.Rooms?.Invite ?? new Dictionary<string, MatrixInvitedRoom>())
        {
            if (!string.IsNullOrWhiteSpace(room.Key))
            {
                invitedRoomIds.Add(room.Key);
            }
        }

        foreach (var room in payload.Rooms?.Join ?? new Dictionary<string, MatrixJoinedRoom>())
        {
            var events = new List<MatrixTimelineEvent>();
            foreach (var timelineEvent in room.Value.Timeline?.Events ?? [])
            {
                var mappedEvent = timelineEvent.ToMatrixTimelineEvent();
                if (mappedEvent is not null)
                {
                    events.Add(mappedEvent);
                }
            }

            if (events.Count > 0)
            {
                rooms.Add(new MatrixTimelineRoom(
                    room.Key,
                    events,
                    room.Value.Summary.GetMemberCount(),
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

        var payload = await response.Content.ReadFromJsonAsync<MatrixJoinedMembersResponse>(JsonOptions, cancellationToken)
            ?? new MatrixJoinedMembersResponse();

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

        var payload = await response.Content.ReadFromJsonAsync<List<MatrixRoomStateEventPayload>>(JsonOptions, cancellationToken)
            ?? [];

        var roomName = payload
            .Where(item => string.Equals(item.Type, "m.room.name", StringComparison.Ordinal))
            .Select(item => item.Content.GetOptionalStringProperty("name"))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (!string.IsNullOrWhiteSpace(roomName))
        {
            return roomName;
        }

        var canonicalAlias = payload
            .Where(item => string.Equals(item.Type, "m.room.canonical_alias", StringComparison.Ordinal))
            .Select(item => item.Content.GetOptionalStringProperty("alias"))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (!string.IsNullOrWhiteSpace(canonicalAlias))
        {
            return canonicalAlias;
        }

        var memberDisplayNames = payload
            .Where(item => string.Equals(item.Type, "m.room.member", StringComparison.Ordinal))
            .Where(item => string.Equals(item.Content.GetOptionalStringProperty("membership"), "join", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(item.Content.GetOptionalStringProperty("membership"), "invite", StringComparison.OrdinalIgnoreCase))
            .Where(item => !string.Equals(item.StateKey, currentMatrixUserId, StringComparison.Ordinal))
            .Select(item => item.Content.GetOptionalStringProperty("displayname"))
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

    [GeneratedRegex(@"https?://\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex UrlRegex();
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
