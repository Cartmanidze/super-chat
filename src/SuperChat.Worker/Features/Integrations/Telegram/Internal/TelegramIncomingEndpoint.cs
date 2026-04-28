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
        [FromServices] ILogger<IncomingPayload> logger,
        CancellationToken cancellationToken)
    {
        var options = optionsAccessor.Value;
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.HmacSecret))
        {
            return Results.NotFound();
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

        using var bodyReader = new StreamReader(httpContext.Request.Body, Encoding.UTF8);
        var rawBody = await bodyReader.ReadToEndAsync(cancellationToken);

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

        var stored = await normalizationService.TryStoreAsync(
            payload.UserId,
            ChatSourceKind.Telegram.ToSourceLabel(),
            payload.ExternalChatId,
            payload.ExternalMessageId,
            payload.SenderName,
            payload.Text,
            payload.SentAt,
            cancellationToken,
            chatTitle: payload.ChatTitle);

        SuperChatMetrics.ChatMessagesByPathTotal
            .WithLabels("userbot", stored ? "stored" : "duplicate")
            .Inc();

        return Results.Ok();
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
            if (!byte.TryParse(hex.Slice(i * 2, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var value))
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
        [property: JsonPropertyName("chat_title")] string? ChatTitle = null);
}
