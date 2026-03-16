using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

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
                new TextEnrichmentRequest(
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

            return new TextEnrichmentResponse(
                NormalizeText(payload.CounterpartyName),
                NormalizeText(payload.OrganizationName),
                payload.Entities?
                    .Where(entity => !string.IsNullOrWhiteSpace(entity.Text) && !string.IsNullOrWhiteSpace(entity.Type))
                    .Select(entity => new TextEnrichmentEntity(
                        entity.Text.Trim(),
                        entity.Type.Trim(),
                        NormalizeText(entity.NormalizedText)))
                    .ToList() ?? [],
                payload.TemporalExpressions?
                    .Where(item => !string.IsNullOrWhiteSpace(item.Text))
                    .Select(item => new TextEnrichmentTemporalExpression(
                        item.Text.Trim(),
                        NormalizeText(item.Value),
                        NormalizeText(item.Grain)))
                    .ToList() ?? []);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Text enrichment request failed.");
            return null;
        }
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private sealed record TextEnrichmentRequest(
        string Text,
        string ReferenceTimeUtc,
        string TimeZoneId);

    private sealed record TextEnrichmentResponseDto(
        string? CounterpartyName,
        string? OrganizationName,
        List<TextEnrichmentEntityDto>? Entities,
        List<TextEnrichmentTemporalExpressionDto>? TemporalExpressions);

    private sealed record TextEnrichmentEntityDto(
        string Text,
        string Type,
        string? NormalizedText);

    private sealed record TextEnrichmentTemporalExpressionDto(
        string Text,
        string? Value,
        string? Grain);
}
