namespace SuperChat.Infrastructure.Services;

internal sealed record TextEnrichmentRequestDto(
    string Text,
    string ReferenceTimeUtc,
    string TimeZoneId);

internal sealed record TextEnrichmentResponseDto(
    string? CounterpartyName,
    string? OrganizationName,
    List<TextEnrichmentEntityDto>? Entities,
    List<TextEnrichmentTemporalExpressionDto>? TemporalExpressions);

internal sealed record TextEnrichmentEntityDto(
    string Text,
    string Type,
    string? NormalizedText);

internal sealed record TextEnrichmentTemporalExpressionDto(
    string Text,
    string? Value,
    string? Grain);
