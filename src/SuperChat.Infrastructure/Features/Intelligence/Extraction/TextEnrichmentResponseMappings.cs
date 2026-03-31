using SuperChat.Contracts.Features.Intelligence.Extraction;

namespace SuperChat.Infrastructure.Features.Intelligence.Extraction;

internal static class TextEnrichmentResponseMappings
{
    public static TextEnrichmentResponse ToTextEnrichmentResponse(this TextEnrichmentResponseDto payload)
    {
        return new TextEnrichmentResponse(
            NormalizeText(payload.CounterpartyName),
            NormalizeText(payload.OrganizationName),
            payload.Entities?
                .Where(entity => !string.IsNullOrWhiteSpace(entity.Text) && !string.IsNullOrWhiteSpace(entity.Type))
                .Select(entity => entity.ToTextEnrichmentEntity())
                .ToList() ?? [],
            payload.TemporalExpressions?
                .Where(item => !string.IsNullOrWhiteSpace(item.Text))
                .Select(item => item.ToTextEnrichmentTemporalExpression())
                .ToList() ?? []);
    }

    public static TextEnrichmentEntity ToTextEnrichmentEntity(this TextEnrichmentEntityDto entity)
    {
        return new TextEnrichmentEntity(
            entity.Text.Trim(),
            entity.Type.Trim(),
            NormalizeText(entity.NormalizedText));
    }

    public static TextEnrichmentTemporalExpression ToTextEnrichmentTemporalExpression(
        this TextEnrichmentTemporalExpressionDto item)
    {
        return new TextEnrichmentTemporalExpression(
            item.Text.Trim(),
            NormalizeText(item.Value),
            NormalizeText(item.Grain));
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
