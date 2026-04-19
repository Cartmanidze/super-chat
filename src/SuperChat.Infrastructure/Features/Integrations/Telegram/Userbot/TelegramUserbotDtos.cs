using System.Text.Json.Serialization;

namespace SuperChat.Infrastructure.Features.Integrations.Telegram.Userbot;

internal sealed record StartConnectRequest(
    [property: JsonPropertyName("phone")] string Phone);

internal sealed record StartConnectResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("phone_code_hash")] string? PhoneCodeHash);

internal sealed record SubmitCodeRequest(
    [property: JsonPropertyName("code")] string Code);

internal sealed record SubmitCodeResponse(
    [property: JsonPropertyName("status")] string Status);

internal sealed record SubmitPasswordRequest(
    [property: JsonPropertyName("password")] string Password);

internal sealed record SessionStatusResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("phone")] string? Phone,
    [property: JsonPropertyName("telegram_user_id")] long? TelegramUserId);

public enum TelegramUserbotConnectStatus
{
    Unknown = 0,
    NotStarted = 1,
    AwaitingCode = 2,
    AwaitingPassword = 3,
    Connected = 4,
    Failed = 5
}
