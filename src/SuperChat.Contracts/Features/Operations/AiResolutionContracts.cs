using System.Text.Json.Serialization;

namespace SuperChat.Contracts.Features.Operations;

public sealed record AiResolutionResponse(
    [property: JsonPropertyName("decisions")] IReadOnlyList<AiResolutionDecision>? Decisions);

public sealed record AiResolutionDecision(
    [property: JsonPropertyName("candidate_id")] string CandidateId,
    [property: JsonPropertyName("should_resolve")] bool ShouldResolve,
    [property: JsonPropertyName("resolution_kind")] string? ResolutionKind,
    [property: JsonPropertyName("confidence")] double? Confidence,
    [property: JsonPropertyName("resolved_at_utc")] string? ResolvedAtUtc,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("evidence_message_ids")] IReadOnlyList<string>? EvidenceMessageIds);
