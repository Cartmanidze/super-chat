using System.Text.RegularExpressions;

namespace SuperChat.Domain.Features.Intelligence;

public sealed record ResolutionCandidateInput(
    Guid Id,
    ExtractedItemKind Kind,
    string Title,
    string Summary,
    string? Person,
    DateTimeOffset ObservedAt,
    DateTimeOffset? DueAt,
    IReadOnlyList<ResolutionEvidenceMessageInput> LaterMessages);

public sealed record ResolutionEvidenceMessageInput(
    string SenderName,
    string Text,
    DateTimeOffset SentAt);

public sealed record ResolutionCandidateSelectionResult(
    Guid Id,
    double Score);

public static class ResolutionCandidateSelection
{
    private static readonly string[] MeetingKeywords =
    [
        "после встречи", "после созвона", "по итогам встречи", "по итогам созвона",
        "thanks for the call", "thanks for the meeting", "after the call", "after the meeting",
        "перенес", "перенесли", "reschedule", "cancel", "отмен", "перенос"
    ];

    public static IReadOnlyList<ResolutionCandidateSelectionResult> SelectTopCandidates(
        IReadOnlyList<ResolutionCandidateInput> candidates,
        DateTimeOffset now,
        int maxCandidates)
    {
        return candidates
            .Select(item => new ResolutionCandidateSelectionResult(item.Id, Score(item, now)))
            .Where(item => item.Score > 0d)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Id)
            .Take(Math.Max(1, maxCandidates))
            .ToList();
    }

    private static double Score(ResolutionCandidateInput candidate, DateTimeOffset now)
    {
        if (candidate.Kind != ExtractedItemKind.Meeting)
        {
            return 0d;
        }

        if (candidate.LaterMessages.Count == 0)
        {
            return candidate.DueAt is not null && candidate.DueAt <= now
                ? 0.6d
                : 0d;
        }

        var score = 0d;
        var candidateTerms = ExtractTerms(candidate.Title)
            .Concat(ExtractTerms(candidate.Summary))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var message in candidate.LaterMessages)
        {
            var lowered = message.Text.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lowered))
            {
                continue;
            }

            score += ScoreMeeting(candidate, lowered, message, candidateTerms, now);
        }

        return Math.Clamp(score, 0d, 1d);
    }

    private static double ScoreMeeting(
        ResolutionCandidateInput candidate,
        string lowered,
        ResolutionEvidenceMessageInput message,
        HashSet<string> candidateTerms,
        DateTimeOffset now)
    {
        var score = 0.1d;

        if (candidate.DueAt is not null && message.SentAt >= candidate.DueAt.Value)
        {
            score += 0.15d;
        }

        if (candidate.DueAt is not null && candidate.DueAt <= now)
        {
            score += 0.1d;
        }

        if (ContainsAny(lowered, MeetingKeywords))
        {
            score += 0.45d;
        }

        score += ComputeTermOverlapScore(lowered, candidateTerms);
        return score;
    }

    private static double ComputeTermOverlapScore(string text, HashSet<string> candidateTerms)
    {
        if (candidateTerms.Count == 0)
        {
            return 0d;
        }

        var textTerms = ExtractTerms(text).ToHashSet(StringComparer.Ordinal);
        if (textTerms.Count == 0)
        {
            return 0d;
        }

        var overlap = textTerms.Count(term => candidateTerms.Contains(term));
        if (overlap == 0)
        {
            return 0d;
        }

        return Math.Min(0.25d, overlap * 0.06d);
    }

    private static IEnumerable<string> ExtractTerms(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return Regex.Matches(text.ToLowerInvariant(), "[\\p{L}\\p{N}]{3,}")
            .Select(match => match.Value)
            .Where(term => term is not "что" and not "это" and not "для" and not "with" and not "from" and not "that");
    }

    private static bool ContainsAny(string text, IEnumerable<string> values)
    {
        return values.Any(value => text.Contains(value, StringComparison.Ordinal));
    }
}
