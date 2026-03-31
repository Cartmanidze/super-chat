using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Contracts.Features.Intelligence.Extraction;
using SuperChat.Contracts.Features.Intelligence.Retrieval;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Infrastructure.Features.Intelligence.Resolution;

internal sealed class DeepSeekResolutionService(
    IDeepSeekJsonClient deepSeekJsonClient,
    IOptions<DeepSeekOptions> deepSeekOptions,
    IOptions<ResolutionOptions> resolutionOptions,
    PilotOptions pilotOptions,
    ILogger<DeepSeekResolutionService> logger) : IAiResolutionService
{
    public async Task<IReadOnlyList<AiResolutionDecisionResult>> ResolveAsync(
        IReadOnlyList<ConversationResolutionCandidate> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0 ||
            !resolutionOptions.Value.UseLlm ||
            !deepSeekJsonClient.IsConfigured)
        {
            return Array.Empty<AiResolutionDecisionResult>();
        }

        var timeZone = ResolveReferenceTimeZone(pilotOptions.TodayTimeZoneId);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var options = resolutionOptions.Value;
            var response = await deepSeekJsonClient.CompleteJsonAsync<AiResolutionResponse>(
                ConversationResolutionPromptBuilder.BuildMessages(candidates, timeZone, options.MinConfidence),
                Math.Max(1, options.MaxOutputTokens),
                cancellationToken);

            return MapResponse(
                candidates,
                response,
                options.MinConfidence,
                string.IsNullOrWhiteSpace(deepSeekOptions.Value.Model) ? null : deepSeekOptions.Value.Model.Trim());
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "AI resolution failed for {CandidateCount} candidates after {ElapsedMs} ms.", candidates.Count, stopwatch.ElapsedMilliseconds);
            return Array.Empty<AiResolutionDecisionResult>();
        }
    }

    private static IReadOnlyList<AiResolutionDecisionResult> MapResponse(
        IReadOnlyList<ConversationResolutionCandidate> candidates,
        AiResolutionResponse? response,
        double minConfidence,
        string? model)
    {
        if (response?.Decisions is null || response.Decisions.Count == 0)
        {
            return Array.Empty<AiResolutionDecisionResult>();
        }

        var candidatesById = candidates.ToDictionary(item => item.Id.ToString("D"), StringComparer.OrdinalIgnoreCase);
        var results = new List<AiResolutionDecisionResult>(response.Decisions.Count);
        foreach (var decision in response.Decisions)
        {
            if (!decision.ShouldResolve ||
                string.IsNullOrWhiteSpace(decision.CandidateId) ||
                !candidatesById.TryGetValue(decision.CandidateId, out var candidate))
            {
                continue;
            }

            var confidence = Math.Clamp(decision.Confidence ?? 0d, 0d, 1d);
            if (confidence < minConfidence)
            {
                continue;
            }

            var resolutionKind = NormalizeResolutionKind(decision.ResolutionKind, candidate);
            if (resolutionKind is null)
            {
                continue;
            }

            var resolutionSource = candidate.Kind switch
            {
                ExtractedItemKind.WaitingOn => WorkItemResolutionState.AutoAiReply,
                ExtractedItemKind.Meeting => WorkItemResolutionState.AutoAiMeetingCompletion,
                _ => WorkItemResolutionState.AutoAiCompletion
            };

            results.Add(new AiResolutionDecisionResult(
                candidate.Id,
                resolutionKind,
                resolutionSource,
                ParseResolvedAt(decision.ResolvedAtUtc, candidate),
                confidence,
                model,
                decision.EvidenceMessageIds?
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToList()));
        }

        return results;
    }

    private static string? NormalizeResolutionKind(string? rawValue, ConversationResolutionCandidate candidate)
    {
        var normalized = rawValue?.Trim().ToLowerInvariant();
        return normalized switch
        {
            WorkItemResolutionState.Completed => WorkItemResolutionState.Completed,
            WorkItemResolutionState.Missed when candidate.Kind == ExtractedItemKind.Meeting => WorkItemResolutionState.Missed,
            WorkItemResolutionState.Cancelled when candidate.Kind == ExtractedItemKind.Meeting => WorkItemResolutionState.Cancelled,
            WorkItemResolutionState.Rescheduled when candidate.Kind == ExtractedItemKind.Meeting => WorkItemResolutionState.Rescheduled,
            _ => null
        };
    }

    private static DateTimeOffset ParseResolvedAt(string? rawValue, ConversationResolutionCandidate candidate)
    {
        if (!string.IsNullOrWhiteSpace(rawValue) &&
            DateTimeOffset.TryParse(rawValue, out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        return candidate.LaterMessages.LastOrDefault()?.SentAt ?? candidate.DueAt ?? candidate.ObservedAt;
    }

    private static TimeZoneInfo ResolveReferenceTimeZone(string configuredTimeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(configuredTimeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(configuredTimeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}
