namespace SuperChat.Contracts.Features.Intelligence.Extraction;

public interface ITextEnrichmentClient
{
    bool IsConfigured { get; }

    Task<TextEnrichmentResponse?> EnrichAsync(
        string text,
        DateTimeOffset referenceTimeUtc,
        string timeZoneId,
        CancellationToken cancellationToken);
}

public sealed record TextEnrichmentResponse(
    string? CounterpartyName,
    string? OrganizationName,
    IReadOnlyList<TextEnrichmentEntity> Entities,
    IReadOnlyList<TextEnrichmentTemporalExpression> TemporalExpressions);

public sealed record TextEnrichmentEntity(
    string Text,
    string Type,
    string? NormalizedText);

public sealed record TextEnrichmentTemporalExpression(
    string Text,
    string? Value,
    string? Grain);
