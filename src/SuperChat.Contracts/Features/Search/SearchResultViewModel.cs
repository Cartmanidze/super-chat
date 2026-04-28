namespace SuperChat.Contracts.Features.Search;

public sealed record SearchResultViewModel(
    string Title,
    string Summary,
    string Kind,
    string ChatTitle,
    DateTimeOffset ObservedAt,
    string? ResolutionNote = null,
    double? ResolutionConfidence = null);
