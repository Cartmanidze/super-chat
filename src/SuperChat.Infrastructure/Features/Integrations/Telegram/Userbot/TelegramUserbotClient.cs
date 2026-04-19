using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace SuperChat.Infrastructure.Features.Integrations.Telegram.Userbot;

public sealed class TelegramUserbotClient(
    HttpClient httpClient,
    ILogger<TelegramUserbotClient> logger)
{
    public async Task<StartConnectResult> StartConnectAsync(Guid userId, string phone, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"sessions/{userId:N}/connect",
            new StartConnectRequest(phone),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Telegram userbot service returned non-success on connect. UserId={UserId}, StatusCode={StatusCode}.",
                userId,
                (int)response.StatusCode);
            return new StartConnectResult(Success: false, PhoneCodeHash: null);
        }

        var payload = await response.Content.ReadFromJsonAsync<StartConnectResponse>(cancellationToken);
        return new StartConnectResult(
            Success: payload is not null,
            PhoneCodeHash: payload?.PhoneCodeHash);
    }

    public async Task<SubmitCodeResult> SubmitCodeAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"sessions/{userId:N}/code",
            new SubmitCodeRequest(code),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            return new SubmitCodeResult(Status: TelegramUserbotConnectStatus.AwaitingPassword);
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Telegram userbot service rejected code. UserId={UserId}, StatusCode={StatusCode}.",
                userId,
                (int)response.StatusCode);
            return new SubmitCodeResult(Status: TelegramUserbotConnectStatus.Failed);
        }

        var payload = await response.Content.ReadFromJsonAsync<SubmitCodeResponse>(cancellationToken);
        return new SubmitCodeResult(Status: ParseConnectStatus(payload?.Status));
    }

    public async Task<TelegramUserbotConnectStatus> SubmitPasswordAsync(
        Guid userId,
        string password,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"sessions/{userId:N}/password",
            new SubmitPasswordRequest(password),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Telegram userbot service rejected password. UserId={UserId}, StatusCode={StatusCode}.",
                userId,
                (int)response.StatusCode);
            return TelegramUserbotConnectStatus.Failed;
        }

        var payload = await response.Content.ReadFromJsonAsync<SubmitCodeResponse>(cancellationToken);
        return ParseConnectStatus(payload?.Status);
    }

    public async Task DisconnectAsync(Guid userId, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsync($"sessions/{userId:N}/disconnect", content: null, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
        {
            logger.LogWarning(
                "Telegram userbot disconnect returned non-success. UserId={UserId}, StatusCode={StatusCode}.",
                userId,
                (int)response.StatusCode);
        }
    }

    public async Task<TelegramUserbotConnectStatus> GetStatusAsync(Guid userId, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"sessions/{userId:N}/status", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return TelegramUserbotConnectStatus.NotStarted;
        }

        if (!response.IsSuccessStatusCode)
        {
            return TelegramUserbotConnectStatus.Unknown;
        }

        var payload = await response.Content.ReadFromJsonAsync<SessionStatusResponse>(cancellationToken);
        return ParseConnectStatus(payload?.Status);
    }

    internal static TelegramUserbotConnectStatus ParseConnectStatus(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "not_started" => TelegramUserbotConnectStatus.NotStarted,
            "awaiting_code" => TelegramUserbotConnectStatus.AwaitingCode,
            "awaiting_password" => TelegramUserbotConnectStatus.AwaitingPassword,
            "connected" => TelegramUserbotConnectStatus.Connected,
            "failed" => TelegramUserbotConnectStatus.Failed,
            _ => TelegramUserbotConnectStatus.Unknown
        };
    }
}

public sealed record StartConnectResult(bool Success, string? PhoneCodeHash);

public sealed record SubmitCodeResult(TelegramUserbotConnectStatus Status);
