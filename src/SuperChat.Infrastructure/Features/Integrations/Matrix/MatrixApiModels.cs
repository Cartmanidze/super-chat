using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuperChat.Infrastructure.Services;

internal sealed record MatrixEnsureUserRequest(
    string Password,
    bool Admin,
    bool Deactivated,
    [property: JsonPropertyName("logout_devices")] bool LogoutDevices);

internal sealed record MatrixCreateDirectRoomRequest(
    IReadOnlyList<string> Invite,
    [property: JsonPropertyName("is_direct")] bool IsDirect,
    string Preset,
    string Name);

internal sealed record MatrixEmptyRequest;

internal sealed record MatrixSendTextMessageRequest(
    [property: JsonPropertyName("msgtype")] string MessageType,
    string Body);

internal sealed record MatrixAdminLoginResponse(
    [property: JsonPropertyName("access_token")] string? AccessToken);

internal sealed record MatrixCreateRoomResponse(
    [property: JsonPropertyName("room_id")] string? RoomId);

internal sealed class MatrixSyncResponse
{
    [JsonPropertyName("next_batch")]
    public string? NextBatch { get; init; }

    [JsonPropertyName("account_data")]
    public MatrixAccountDataPayload? AccountData { get; init; }

    public MatrixSyncRooms? Rooms { get; init; }
}

internal sealed class MatrixSyncRooms
{
    public Dictionary<string, MatrixJoinedRoom>? Join { get; init; }

    public Dictionary<string, MatrixInvitedRoom>? Invite { get; init; }
}

internal sealed class MatrixJoinedRoom
{
    public MatrixRoomSummaryPayload? Summary { get; init; }

    public MatrixTimelinePayload? Timeline { get; init; }
}

internal sealed class MatrixInvitedRoom;

internal sealed class MatrixAccountDataPayload
{
    public List<MatrixAccountDataEventPayload>? Events { get; init; }
}

internal sealed class MatrixAccountDataEventPayload
{
    public string? Type { get; init; }

    public JsonElement? Content { get; init; }
}

internal sealed class MatrixRoomSummaryPayload
{
    [JsonPropertyName("m.joined_member_count")]
    public int? JoinedMemberCount { get; init; }

    [JsonPropertyName("m.invited_member_count")]
    public int? InvitedMemberCount { get; init; }
}

internal sealed class MatrixTimelinePayload
{
    public List<MatrixTimelineEventPayload>? Events { get; init; }
}

internal sealed class MatrixTimelineEventPayload
{
    [JsonPropertyName("event_id")]
    public string? EventId { get; init; }

    public string? Type { get; init; }

    public string? Sender { get; init; }

    [JsonPropertyName("origin_server_ts")]
    public long? OriginServerTs { get; init; }

    public JsonElement? Content { get; init; }
}

internal sealed class MatrixJoinedMembersResponse
{
    public Dictionary<string, object>? Joined { get; init; }
}

internal sealed class MatrixRoomStateEventPayload
{
    public string? Type { get; init; }

    [JsonPropertyName("state_key")]
    public string? StateKey { get; init; }

    public JsonElement? Content { get; init; }
}

internal sealed record MatrixRoomMemberStatePayload(
    [property: JsonPropertyName("membership")] string? Membership);
