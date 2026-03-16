using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using SuperChat.Contracts;
using SuperChat.Contracts.Configuration;
using SuperChat.Domain.Model;
using SuperChat.Domain.Services;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Diagnostics;

namespace SuperChat.Infrastructure.Services;

public sealed class DeepSeekStructuredExtractionService(
    IDeepSeekJsonClient deepSeekJsonClient,
    PilotOptions pilotOptions,
    ILogger<DeepSeekStructuredExtractionService> logger) : IAiStructuredExtractionService
{
    private const int MaxOutputTokens = 550;

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

        var referenceTimeZone = ResolveReferenceTimeZone(pilotOptions.TodayTimeZoneId);
        var stopwatch = Stopwatch.StartNew();
        AiPipelineLog.StructuredExtractionStarted(
            logger,
            window.Source,
            transcript.Length,
            window.Messages.Count);

        var usedFallback = false;

        try
        {
            var aiItems = await TryExtractViaAiAsync(window, transcript, referenceTimeZone, cancellationToken);
            if (aiItems.Count > 0)
            {
                stopwatch.Stop();
                AiPipelineLog.StructuredExtractionCompleted(
                    logger,
                    window.Source,
                    aiItems.Count,
                    usedFallback,
                    stopwatch.ElapsedMilliseconds);
                return aiItems;
            }
        }
        catch (Exception exception)
        {
            AiPipelineLog.StructuredExtractionFailed(
                logger,
                window.Source,
                transcript.Length,
                stopwatch.ElapsedMilliseconds,
                exception);
        }

        usedFallback = true;
        var fallbackItems = await BuildHeuristicFallbackAsync(window, referenceTimeZone, cancellationToken);

        stopwatch.Stop();
        AiPipelineLog.StructuredExtractionCompleted(
            logger,
            window.Source,
            fallbackItems.Count,
            usedFallback,
            stopwatch.ElapsedMilliseconds);

        return fallbackItems;
    }

    private async Task<IReadOnlyCollection<ExtractedItem>> TryExtractViaAiAsync(
        ConversationWindow window,
        string transcript,
        TimeZoneInfo referenceTimeZone,
        CancellationToken cancellationToken)
    {
        if (!deepSeekJsonClient.IsConfigured)
        {
            return Array.Empty<ExtractedItem>();
        }

        var sentAtLocal = TimeZoneInfo.ConvertTime(window.TsTo, referenceTimeZone);
        var response = await deepSeekJsonClient.CompleteJsonAsync<DeepSeekStructuredResponse>(
            [
                new DeepSeekMessage("system", BuildSystemPrompt(referenceTimeZone.Id)),
                new DeepSeekMessage("user", BuildUserPrompt(window, sentAtLocal, transcript))
            ],
            MaxOutputTokens,
            cancellationToken);

        var extractedItems = MapStructuredItems(window, response?.Items, sentAtLocal, referenceTimeZone);
        var deterministicMeeting = MeetingSignalDetector.TryFromChunk(
            transcript,
            window.TsFrom,
            window.TsTo,
            referenceTimeZone);

        if (deterministicMeeting is not null)
        {
            MergeDeterministicMeeting(window, extractedItems, deterministicMeeting);
        }

        HeuristicStructuredExtractionService.ApplyWaitingOnWindowRules(window, extractedItems);
        return extractedItems;
    }

    private static async Task<IReadOnlyCollection<ExtractedItem>> BuildHeuristicFallbackAsync(
        ConversationWindow window,
        TimeZoneInfo referenceTimeZone,
        CancellationToken cancellationToken)
    {
        var items = new List<ExtractedItem>();

        foreach (var message in window.Messages)
        {
            var extracted = await HeuristicStructuredExtractionService.ExtractCoreAsync(
                message,
                referenceTimeZone,
                cancellationToken);

            foreach (var item in extracted)
            {
                if (!items.Any(existing =>
                        existing.Kind == item.Kind &&
                        string.Equals(existing.SourceEventId, item.SourceEventId, StringComparison.Ordinal)))
                {
                    items.Add(item);
                }
            }
        }

        HeuristicStructuredExtractionService.ApplyWaitingOnWindowRules(window, items);
        return items;
    }

    private static List<ExtractedItem> MapStructuredItems(
        ConversationWindow window,
        IReadOnlyList<DeepSeekStructuredItem>? items,
        DateTimeOffset sentAtLocal,
        TimeZoneInfo referenceTimeZone)
    {
        var result = new List<ExtractedItem>();
        if (items is null || items.Count == 0)
        {
            return result;
        }

        foreach (var item in items)
        {
            if (!TryMapKind(item.Kind, out var kind))
            {
                continue;
            }

            var summary = string.IsNullOrWhiteSpace(item.Summary)
                ? window.Transcript
                : item.Summary.Trim();

            var title = string.IsNullOrWhiteSpace(item.Title)
                ? BuildDefaultTitle(kind, item.Person)
                : item.Title.Trim();

            if (string.IsNullOrWhiteSpace(summary) || string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var confidence = Math.Clamp(item.Confidence ?? 0.74d, 0d, 1d);
            var dueAt = ParseDeadline(item.Deadline, sentAtLocal, referenceTimeZone);
            var person = string.IsNullOrWhiteSpace(item.Person) ? null : item.Person.Trim();

            result.Add(new ExtractedItem(
                Guid.NewGuid(),
                window.UserId,
                kind,
                title,
                summary,
                window.MatrixRoomId,
                window.LastMessage.MatrixEventId,
                person,
                window.TsTo,
                dueAt,
                confidence));
        }

        return result;
    }

    private static void MergeDeterministicMeeting(
        ConversationWindow window,
        List<ExtractedItem> extractedItems,
        MeetingSignal deterministicMeeting)
    {
        var existingMeetingIndex = extractedItems.FindIndex(item => item.Kind == ExtractedItemKind.Meeting);
        if (existingMeetingIndex < 0)
        {
            extractedItems.Add(new ExtractedItem(
                Guid.NewGuid(),
                window.UserId,
                ExtractedItemKind.Meeting,
                deterministicMeeting.Title,
                deterministicMeeting.Summary,
                window.MatrixRoomId,
                window.LastMessage.MatrixEventId,
                deterministicMeeting.Person,
                window.TsTo,
                deterministicMeeting.ScheduledFor,
                deterministicMeeting.Confidence));
            return;
        }

        var existingMeeting = extractedItems[existingMeetingIndex];
        if (existingMeeting.DueAt is not null)
        {
            return;
        }

        extractedItems[existingMeetingIndex] = existingMeeting with
        {
            DueAt = deterministicMeeting.ScheduledFor,
            Confidence = Math.Max(existingMeeting.Confidence, deterministicMeeting.Confidence)
        };
    }

    private static string BuildSystemPrompt(string timeZoneId)
    {
        return $$"""
            You extract grounded work signals from a short Telegram dialogue window for a productivity product.
            Return JSON only in the shape {"items":[...]}.

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
            - "commitment" means the user promised to do something.
            - "task" means the dialogue contains a concrete requested action or deliverable.
            - "meeting" means the dialogue proposes, confirms, or schedules a meeting/call.
            - Prefer the most useful single item per kind; avoid duplicates.
            - If there is no real signal, return {"items":[]}.
            - title must be short, useful, and in Russian.
            - summary must be concise, in Russian, and grounded in the dialogue.
            - person should contain only an explicit person or counterpart name when present, otherwise null.
            - deadline must be null unless the dialogue gives enough information. If present, return ISO-8601 UTC like 2026-03-13T17:00:00Z.
            - confidence must be a number from 0.0 to 1.0 and should vary based on certainty.
            - Business timezone for interpreting relative dates is {{timeZoneId}}.
            """;
    }

    private static string BuildUserPrompt(
        ConversationWindow window,
        DateTimeOffset sentAtLocal,
        string transcript)
    {
        return $$"""
            Dialogue metadata:
            - source: {{window.Source}}
            - room_id: {{window.MatrixRoomId}}
            - message_count: {{window.Messages.Count}}
            - ts_from_utc: {{window.TsFrom.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)}}
            - ts_to_utc: {{window.TsTo.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)}}
            - ts_to_local: {{sentAtLocal.ToString("O", CultureInfo.InvariantCulture)}}

            Dialogue transcript:
            {{transcript}}
            """;
    }

    private static bool TryMapKind(string? kind, out ExtractedItemKind extractedItemKind)
    {
        var normalized = kind?.Trim().ToLowerInvariant();
        extractedItemKind = normalized switch
        {
            "task" or "todo" or "action" => ExtractedItemKind.Task,
            "commitment" or "promise" => ExtractedItemKind.Commitment,
            "waiting_on" or "waitingon" or "waiting" or "follow_up" => ExtractedItemKind.WaitingOn,
            "meeting" or "call" => ExtractedItemKind.Meeting,
            _ => default
        };

        return normalized is "task" or "todo" or "action" or
            "commitment" or "promise" or
            "waiting_on" or "waitingon" or "waiting" or "follow_up" or
            "meeting" or "call";
    }

    private static string BuildDefaultTitle(ExtractedItemKind kind, string? person)
    {
        return kind switch
        {
            ExtractedItemKind.WaitingOn when !string.IsNullOrWhiteSpace(person) => $"Нужно ответить: {person!.Trim()}",
            ExtractedItemKind.WaitingOn => "Нужно ответить",
            ExtractedItemKind.Commitment => "Ты пообещал",
            ExtractedItemKind.Task => "Нужен следующий шаг",
            ExtractedItemKind.Meeting => "Скоро встреча",
            _ => "Важный сигнал"
        };
    }

    private static DateTimeOffset? ParseDeadline(
        string? value,
        DateTimeOffset sentAtLocal,
        TimeZoneInfo referenceTimeZone)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (DateTimeOffset.TryParse(
                normalized,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsedOffset))
        {
            return parsedOffset.ToUniversalTime();
        }

        if (!DateTime.TryParse(
                normalized,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsedDateTime))
        {
            return null;
        }

        var unspecifiedLocal = DateTime.SpecifyKind(parsedDateTime, DateTimeKind.Unspecified);
        var offset = referenceTimeZone.GetUtcOffset(unspecifiedLocal);
        var localDeadline = new DateTimeOffset(unspecifiedLocal, offset);

        if (localDeadline < sentAtLocal.AddDays(-1))
        {
            return null;
        }

        return localDeadline.ToUniversalTime();
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
