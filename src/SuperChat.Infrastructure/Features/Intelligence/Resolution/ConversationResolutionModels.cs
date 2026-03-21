using SuperChat.Domain.Features.Intelligence;

namespace SuperChat.Infrastructure.Features.Intelligence.Resolution;

internal enum ResolutionCandidateType
{
    WorkItem,
    Meeting
}

internal sealed record ConversationResolutionCandidate(
    Guid Id,
    ResolutionCandidateType CandidateType,
    ExtractedItemKind Kind,
    string Title,
    string Summary,
    string MatrixRoomId,
    string? Person,
    DateTimeOffset ObservedAt,
    DateTimeOffset? DueAt,
    IReadOnlyList<ResolutionMessageSnippet> LaterMessages);

internal sealed record ResolutionMessageSnippet(
    string MatrixEventId,
    string SenderName,
    string Text,
    DateTimeOffset SentAt);

internal sealed record AiResolutionDecisionResult(
    Guid CandidateId,
    string ResolutionKind,
    string ResolutionSource,
    DateTimeOffset ResolvedAt,
    double Confidence,
    string? Model,
    IReadOnlyList<string>? EvidenceMessageIds);
