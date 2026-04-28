using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using SuperChat.Contracts;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Contracts.Features.Intelligence.Extraction;
using SuperChat.Contracts.Features.Intelligence.Retrieval;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Domain.Features.Messaging;
using SuperChat.Infrastructure.Diagnostics;

namespace SuperChat.Infrastructure.Features.Intelligence.Extraction;

public sealed class DeepSeekStructuredExtractionService(
    IDeepSeekJsonClient deepSeekJsonClient,
    HeuristicStructuredExtractionService heuristicService,
    PilotOptions pilotOptions,
    IUserTimeZoneResolver userTimeZoneResolver,
    ILogger<DeepSeekStructuredExtractionService> logger) : IAiStructuredExtractionService
{
    private const int MaxOutputTokens = 900;

    public async Task<IReadOnlyCollection<ExtractedItem>> ExtractAsync(ConversationWindow window, CancellationToken cancellationToken)
    {
        if (window.Messages.Count == 0)
        {
            return Array.Empty<ExtractedItem>();
        }

        var transcript = window.Transcript.Trim();
        if (string.IsNullOrWhiteSpace(transcript) || StructuredArtifactDetector.LooksLikeStructuredArtifact(transcript))
        {
            return Array.Empty<ExtractedItem>();
        }

        var fallbackTimeZone = ResolveReferenceTimeZone(pilotOptions.TodayTimeZoneId);
        var referenceTimeZone = await userTimeZoneResolver.ResolveAsync(window.UserId, fallbackTimeZone, cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        AiPipelineLog.StructuredExtractionStarted(logger, window.Source, transcript.Length, window.Messages.Count);

        try
        {
            var aiAttempt = await TryExtractViaAiAsync(window, transcript, referenceTimeZone, cancellationToken);
            if (aiAttempt.IsAuthoritative)
            {
                return CompleteWith(aiAttempt.Items, usedFallback: false, stopwatch, window.Source);
            }
        }
        catch (Exception exception)
        {
            AiPipelineLog.StructuredExtractionFailed(logger, window.Source, transcript.Length, stopwatch.ElapsedMilliseconds, exception);
        }

        var fallbackItems = await heuristicService.ExtractAsync(window, cancellationToken);
        return CompleteWith(fallbackItems, usedFallback: true, stopwatch, window.Source);
    }

    private IReadOnlyCollection<ExtractedItem> CompleteWith(
        IReadOnlyCollection<ExtractedItem> items,
        bool usedFallback,
        Stopwatch stopwatch,
        string source)
    {
        stopwatch.Stop();
        AiPipelineLog.StructuredExtractionCompleted(logger, source, items.Count, usedFallback, stopwatch.ElapsedMilliseconds);
        return items;
    }

    private async Task<StructuredExtractionAiAttemptResult> TryExtractViaAiAsync(
        ConversationWindow window,
        string transcript,
        TimeZoneInfo referenceTimeZone,
        CancellationToken cancellationToken)
    {
        if (!deepSeekJsonClient.IsConfigured)
        {
            return StructuredExtractionAiAttemptResult.NotAuthoritative;
        }

        var sentAtLocal = TimeZoneInfo.ConvertTime(window.TsTo, referenceTimeZone);
        var latestMeaningfulMessage = WaitingOnTurnDetector.GetLatestMeaningfulMessage(window);
        var unresolvedExternalMessage = WaitingOnTurnDetector.GetUnansweredExternalMessage(window);
        var response = await deepSeekJsonClient.CompleteJsonAsync<DeepSeekStructuredResponse>(
            [
                new DeepSeekMessage("system", BuildSystemPrompt(referenceTimeZone.Id)),
                new DeepSeekMessage("user", BuildUserPrompt(window, sentAtLocal, transcript, latestMeaningfulMessage, unresolvedExternalMessage))
            ],
            MaxOutputTokens,
            cancellationToken);

        // A null payload is "model did not answer" — let the heuristic fallback handle it.
        // An explicit `{"items":[]}` is the authoritative "nothing here" — keep it as is,
        // do NOT enrich with text-based heuristics or the meeting detector would resurrect
        // false positives (e.g. an advert with the verb "встречайте").
        if (response?.Items is null)
        {
            AiPipelineLog.StructuredExtractionAiResponseReceived(logger, window.Source, 0);
            return StructuredExtractionAiAttemptResult.NotAuthoritative;
        }

        AiPipelineLog.StructuredExtractionAiResponseReceived(logger, window.Source, response.Items.Count);

        var mappingResult = response.Items.ToStructuredItemMappingResult(window, sentAtLocal, referenceTimeZone);
        AiPipelineLog.StructuredExtractionItemMappingCompleted(
            logger,
            window.Source,
            response.Items.Count,
            mappingResult.Items.Count,
            mappingResult.UnknownKindCount,
            mappingResult.InvalidContentCount,
            mappingResult.MappedKinds);

        if (response.Items.Count > 0)
        {
            ApplyDeterministicTopUps(mappingResult.Items, transcript, window, referenceTimeZone);
        }

        return StructuredExtractionAiAttemptResult.Authoritative(mappingResult.Items);
    }

    private static void ApplyDeterministicTopUps(
        List<ExtractedItem> items,
        string transcript,
        ConversationWindow window,
        TimeZoneInfo referenceTimeZone)
    {
        var deterministicMeeting = MeetingSignalDetector.TryFromChunk(transcript, window.TsFrom, window.TsTo, referenceTimeZone);
        deterministicMeeting?.MergeInto(items, window);

        HeuristicStructuredExtractionService.ApplyTimeZoneClarificationRules(window, items, referenceTimeZone);
        HeuristicStructuredExtractionService.ApplyWaitingOnWindowRules(window, items);
    }

    private static string BuildSystemPrompt(string timeZoneId)
    {
        return $$"""
            You extract grounded work signals from a short Telegram dialogue window for a productivity product.
            Return JSON only in the shape {"items":[...]}.
            Always return valid JSON without markdown or prose.

            Allowed item kinds:
            - "waiting_on"
            - "commitment"
            - "task"
            - "meeting"

            Rules:
            - Read the whole dialogue window, not one message in isolation.
            - Use only information explicitly grounded in the dialogue.
            - Do not invent people, deadlines, promises, or meetings.
            - "waiting_on" means the user likely owes someone a reply or next step now.
            - Prefer "waiting_on" only when the latest meaningful dialogue turn still belongs to someone other than the user.
            - An explicit inbound question to the user usually counts as "waiting_on" when unanswered_external_turn is true.
            - Introductory recruiter, sales, vendor, or partnership outreach still counts as "waiting_on" when it ends with a direct question and the user has not replied in the same window.
            - "commitment" means the user promised to do something.
            - "task" means the dialogue contains a concrete requested action or deliverable.
            - "meeting" means the dialogue proposes, confirms, or schedules a meeting/call.
            - Treat interview-related phrases (e.g. "interview", "собеседование", "интервью") as meeting signals when they include concrete date/time context.
            - Return {"items":[]} only when ALL strong cues are absent: request/command, unanswered direct question, explicit promise, or meeting/interview scheduling with date/time.
            - If an interview phrase includes concrete date/time, emit a "meeting" item with confidence >= 0.75.
            - Prefer the most useful single item per kind; avoid duplicates.
            - Never output more than 4 items.
            - Every emitted item must include keys: kind, title, person, deadline, priority, confidence, summary.
            - If there is no real signal, return {"items":[]}.
            - title must be short, useful, and in Russian.
            - summary must be concise, in Russian, and grounded in the dialogue.
            - person should contain only an explicit person or counterpart name when present, otherwise null.
            - deadline must be null unless the dialogue gives enough information. If present, return ISO-8601 UTC like 2026-03-13T17:00:00Z.
            - confidence must be a number from 0.0 to 1.0 and should vary based on certainty.
            - Business timezone for interpreting relative dates is {{timeZoneId}}.

            Example JSON output:
            {
              "items": [
                {
                  "kind": "meeting",
                  "title": "Созвон по кандидату",
                  "person": "Марина",
                  "deadline": "2026-03-21T08:00:00Z",
                  "provider": null,
                  "confidence": 0.84,
                  "summary": "Сегодня в 11:00 по Мск назначено собеседование."
                }
              ]
            }
            """;
    }

    private static string BuildUserPrompt(
        ConversationWindow window,
        DateTimeOffset sentAtLocal,
        string transcript,
        ChatMessage? latestMeaningfulMessage,
        ChatMessage? unresolvedExternalMessage)
    {
        var latestMeaningfulSender = latestMeaningfulMessage?.SenderName?.Trim();
        if (string.IsNullOrWhiteSpace(latestMeaningfulSender))
        {
            latestMeaningfulSender = "unknown";
        }

        return $$"""
            Dialogue metadata:
            - source: {{window.Source}}
            - room_id: {{window.ExternalChatId}}
            - message_count: {{window.Messages.Count}}
            - ts_from_utc: {{window.TsFrom.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)}}
            - ts_to_utc: {{window.TsTo.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)}}
            - ts_to_local: {{sentAtLocal.ToString("O", CultureInfo.InvariantCulture)}}
            - latest_meaningful_sender: {{latestMeaningfulSender}}
            - unanswered_external_turn: {{(unresolvedExternalMessage is not null).ToString().ToLowerInvariant()}}

            Dialogue transcript:
            {{transcript}}
            """;
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

    private readonly record struct StructuredExtractionAiAttemptResult(
        IReadOnlyCollection<ExtractedItem> Items,
        bool IsAuthoritative)
    {
        public static StructuredExtractionAiAttemptResult NotAuthoritative { get; } =
            new(Array.Empty<ExtractedItem>(), IsAuthoritative: false);

        public static StructuredExtractionAiAttemptResult Authoritative(IReadOnlyCollection<ExtractedItem> items) =>
            new(items, IsAuthoritative: true);
    }
}
