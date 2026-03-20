using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Intelligence.Extraction;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Features.Intelligence.Extraction;

public sealed class TextEnrichmentClient(
    HttpClient httpClient,
    IOptions<TextEnrichmentOptions> options,
    ILogger<TextEnrichmentClient> logger) : ITextEnrichmentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public bool IsConfigured =>
        options.Value.Enabled &&
        Uri.TryCreate(options.Value.BaseUrl, UriKind.Absolute, out _);

    public async Task<TextEnrichmentResponse?> EnrichAsync(
        string text,
        DateTimeOffset referenceTimeUtc,
        string timeZoneId,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return null;
        }

        var normalizedText = text.Trim();
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return null;
        }

        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                "/analyze",
                new TextEnrichmentRequestDto(
                    normalizedText,
                    referenceTimeUtc.UtcDateTime.ToString("O"),
                    timeZoneId),
                JsonOptions,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<TextEnrichmentResponseDto>(JsonOptions, cancellationToken);
            if (payload is null)
            {
                return null;
            }

            return payload.ToTextEnrichmentResponse();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Text enrichment request failed.");
            return null;
        }
    }
}
