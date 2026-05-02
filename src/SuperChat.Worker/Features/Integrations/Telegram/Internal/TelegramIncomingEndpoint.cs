using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Integrations.Telegram;
using SuperChat.Contracts.Features.Messaging;
using SuperChat.Infrastructure.Diagnostics;

namespace SuperChat.Worker.Features.Integrations.Telegram.Internal;

public static class TelegramIncomingEndpoint
{
    private const string SignatureHeaderName = "X-Superchat-Signature";
    private const string SignaturePrefix = "sha256=";

    // Source-label кешируем константой — раньше дёргали ChatSourceKind.Telegram.ToSourceLabel()
    // на каждый запрос, что давало лишний string allocation. Значение должно совпадать
    // с тем, что вернул бы ChatSourceKindExtensions.ToSourceLabel(ChatSourceKind.Telegram).
    private const string SourceLabel = "telegram";

    // Hard-limit на размер тела (защита от случайного/злонамеренного огромного payload).
    // Реальные сообщения Telegram укладываются в 4–8 KB (с учётом UTF-8). 64 KB — с запасом.
    private const int MaxBodyBytes = 64 * 1024;

    // Допустимое расхождение между timestamp в payload и серверным временем.
    // Защищает от replay-атаки: перехваченный payload нельзя переиграть позже.
    private static readonly TimeSpan SignatureFreshnessWindow = TimeSpan.FromMinutes(5);

    public static IEndpointRouteBuilder MapTelegramInternalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/internal/telegram")
            .WithTags("TelegramInternal")
            .ExcludeFromDescription();

        group.MapPost("/incoming", HandleAsync);

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        [FromServices] IOptions<TelegramUserbotOptions> optionsAccessor,
        [FromServices] IChatMessageStore normalizationService,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var options = optionsAccessor.Value;
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.HmacSecret))
        {
            return Results.NotFound();
        }

        // Hard-limit на размер тела по заголовку Content-Length до того, как
        // мы вообще начнём читать поток.
        if (httpContext.Request.ContentLength is long contentLength && contentLength > MaxBodyBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        if (!httpContext.Request.Headers.TryGetValue(SignatureHeaderName, out var signatureValues) ||
            signatureValues.Count == 0 ||
            string.IsNullOrWhiteSpace(signatureValues[0]))
        {
            return Results.Unauthorized();
        }

        var providedSignature = signatureValues[0]!.Trim();
        if (!providedSignature.StartsWith(SignaturePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Unauthorized();
        }

        // Читаем тело с фиксированным верхним пределом — даже если
        // Content-Length не передан, не дадим неограниченно расти буферу.
        var rawBody = await ReadBodyWithLimitAsync(httpContext.Request.Body, MaxBodyBytes, cancellationToken);
        if (rawBody is null)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        if (!VerifySignature(rawBody, providedSignature.AsSpan(SignaturePrefix.Length), options.HmacSecret))
        {
            return Results.Unauthorized();
        }

        IncomingPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<IncomingPayload>(rawBody);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "invalid_json" });
        }

        if (payload is null ||
            payload.UserId == Guid.Empty ||
            string.IsNullOrWhiteSpace(payload.ExternalChatId) ||
            string.IsNullOrWhiteSpace(payload.ExternalMessageId) ||
            string.IsNullOrWhiteSpace(payload.SenderName) ||
            payload.Text is null)
        {
            return Results.BadRequest(new { error = "invalid_payload" });
        }

        // Replay-protection. Старые/будущие запросы (вне ±5 мин относительно server clock)
        // отвергаем — это блокирует переигровку перехваченного payload-а.
        if (!IsTimestampFresh(payload.Timestamp, timeProvider))
        {
            return Results.Unauthorized();
        }

        // Дедупликация по (user_id, external_chat_id, external_message_id) живёт на стороне
        // IChatMessageStore — там уникальный индекс гарантирует, что повторно отправленный
        // payload не задвоит запись и не создаст дубль pipeline-команды.
        var stored = await normalizationService.TryStoreAsync(
            payload.UserId,
            SourceLabel,
            payload.ExternalChatId,
            payload.ExternalMessageId,
            payload.SenderName,
            payload.Text,
            payload.SentAt,
            cancellationToken,
            chatTitle: payload.ChatTitle,
            isOutgoing: payload.IsOutgoing);

        SuperChatMetrics.ChatMessagesByPathTotal
            .WithLabels("userbot", stored ? "stored" : "duplicate")
            .Inc();

        return Results.Ok();
    }

    internal static async Task<string?> ReadBodyWithLimitAsync(
        Stream body,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[Math.Min(8192, maxBytes)];
        using var memory = new MemoryStream(maxBytes);
        var totalRead = 0;
        int read;
        while ((read = await body.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            totalRead += read;
            if (totalRead > maxBytes)
            {
                return null;
            }
            await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return Encoding.UTF8.GetString(memory.GetBuffer(), 0, (int)memory.Length);
    }

    internal static bool IsTimestampFresh(long? unixSeconds, TimeProvider timeProvider)
    {
        // Совместимость со старыми sidecar-ами: если timestamp не передан, не блокируем.
        // После выкатки нового sidecar поле всегда есть — старая ветка станет dead code.
        if (unixSeconds is null)
        {
            return true;
        }

        var now = timeProvider.GetUtcNow();
        var sent = DateTimeOffset.FromUnixTimeSeconds(unixSeconds.Value);
        return (now - sent).Duration() <= SignatureFreshnessWindow;
    }

    internal static bool VerifySignature(string rawBody, ReadOnlySpan<char> providedHex, string secret)
    {
        var providedBytes = ConvertHexSpanToBytes(providedHex);
        if (providedBytes is null)
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));

        return CryptographicOperations.FixedTimeEquals(expected, providedBytes);
    }

    internal static string ComputeSignature(string rawBody, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static byte[]? ConvertHexSpanToBytes(ReadOnlySpan<char> hex)
    {
        if (hex.Length == 0 || hex.Length % 2 != 0)
        {
            return null;
        }

        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            if (!byte.TryParse(hex.Slice(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            {
                return null;
            }

            bytes[i] = value;
        }

        return bytes;
    }

    internal sealed record IncomingPayload(
        [property: JsonPropertyName("user_id")] Guid UserId,
        [property: JsonPropertyName("external_chat_id")] string ExternalChatId,
        [property: JsonPropertyName("external_message_id")] string ExternalMessageId,
        [property: JsonPropertyName("sender_name")] string SenderName,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("sent_at")] DateTimeOffset SentAt,
        [property: JsonPropertyName("chat_title")] string? ChatTitle = null,
        [property: JsonPropertyName("is_outgoing")] bool IsOutgoing = false,
        [property: JsonPropertyName("timestamp")] long? Timestamp = null);
}
