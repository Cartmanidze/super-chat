namespace SuperChat.Domain.Features.Intelligence;

public sealed record ResolutionTrace(
    double? Confidence,
    string? Model,
    IReadOnlyList<string>? EvidenceMessageIds);
