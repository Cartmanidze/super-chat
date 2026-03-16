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
    private const int MaxOutputTokens = 450;

    public async Task<IReadOnlyCollection<ExtractedItem>> ExtractAsync(NormalizedMessage message, CancellationToken cancellationToken)
    {
        var text = message.Text.Trim();
        if (string.IsNullOrWhiteSpace(text) || StructuredArtifactDetector.LooksLikeStructuredArtifact(text))
        {
            return Array.Empty<ExtractedItem>();
        }

        var referenceTimeZone = ResolveReferenceTimeZone(pilotOptions.TodayTimeZoneId);
        var stopwatch = Stopwatch.StartNew();
        AiPipelineLog.StructuredExtractionStarted(
            logger,
            message.Source,
            text.Length,
            message.SenderName?.Length ?? 0);

        var usedFallback = false;

        try
        {
            var aiItems = await TryExtractViaAiAsync(message, text, referenceTimeZone, cancellationToken);
            if (aiItems.Count > 0)
            {
                stopwatch.Stop();
                AiPipelineLog.StructuredExtractionCompleted(
                    logger,
                    message.Source,
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
                message.Source,
                text.Length,
                stopwatch.ElapsedMilliseconds,
                exception);
        }

        usedFallback = true;
        var fallbackItems = await HeuristicStructuredExtractionService.ExtractCoreAsync(
            message,
            referenceTimeZone,
            cancellationToken);

        stopwatch.Stop();
        AiPipelineLog.StructuredExtractionCompleted(
            logger,
            message.Source,
            fallbackItems.Count,
            usedFallback,
            stopwatch.ElapsedMilliseconds);

        return fallbackItems;
    }

    private async Task<IReadOnlyCollection<ExtractedItem>> TryExtractViaAiAsync(
        NormalizedMessage message,
        string text,
        TimeZoneInfo referenceTimeZone,
        CancellationToken cancellationToken)
    {
        if (!deepSeekJsonClient.IsConfigured)
        {
            return Array.Empty<ExtractedItem>();
        }

        var sentAtLocal = TimeZoneInfo.ConvertTime(message.SentAt, referenceTimeZone);
        var response = await deepSeekJsonClient.CompleteJsonAsync<DeepSeekStructuredResponse>(
            [
                new DeepSeekMessage("system", BuildSystemPrompt(referenceTimeZone.Id)),
                new DeepSeekMessage("user", BuildUserPrompt(message, text, sentAtLocal))
            ],
            MaxOutputTokens,
            cancellationToken);

        var extractedItems = MapStructuredItems(message, response?.Items, sentAtLocal, referenceTimeZone);
        var deterministicMeeting = MeetingSignalDetector.TryFromMessage(message, referenceTimeZone);

        if (deterministicMeeting is not null)
        {
            MergeDeterministicMeeting(message, extractedItems, deterministicMeeting);
        }

        return extractedItems;
    }

    private static List<ExtractedItem> MapStructuredItems(
        NormalizedMessage message,
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
                ? message.Text.Trim()
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
                message.UserId,
                kind,
                title,
                summary,
                message.MatrixRoomId,
                message.MatrixEventId,
                person,
                message.SentAt,
                dueAt,
                confidence));
        }

        return result;
    }

    private static void MergeDeterministicMeeting(
        NormalizedMessage message,
        List<ExtractedItem> extractedItems,
        MeetingSignal deterministicMeeting)
    {
        var existingMeetingIndex = extractedItems.FindIndex(item => item.Kind == ExtractedItemKind.Meeting);
        if (existingMeetingIndex < 0)
        {
            extractedItems.Add(new ExtractedItem(
                Guid.NewGuid(),
                message.UserId,
                ExtractedItemKind.Meeting,
                deterministicMeeting.Title,
                deterministicMeeting.Summary,
                message.MatrixRoomId,
                message.MatrixEventId,
                deterministicMeeting.Person,
                message.SentAt,
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
            You extract grounded work signals from a single Telegram message for a productivity product.
            Return JSON only in the shape {"items":[...]}.

            Allowed item kinds:
            - "waiting_on"
            - "commitment"
            - "task"
            - "meeting"

            Rules:
            - Use only information explicitly grounded in the message.
            - Do not invent people, deadlines, or commitments.
            - "waiting_on" means the user likely owes someone a reply or next step now.
            - "commitment" means the sender of the message promises to do something.
            - "task" means the message contains a concrete requested action or deliverable.
            - "meeting" means the message proposes, confirms, or schedules a meeting/call.
            - If there is no real signal, return {"items":[]}.
            - Avoid duplicates.
            - title must be short, useful, and in Russian.
            - summary must be a concise Russian summary grounded in the message text.
            - person should contain only an explicit person or counterpart name when present, otherwise null.
            - deadline must be null unless the message gives enough information. If present, return ISO-8601 UTC like 2026-03-13T17:00:00Z.
            - confidence must be a number from 0.0 to 1.0 and should vary based on certainty.
            - Business timezone for interpreting relative dates is {{timeZoneId}}.
            """;
    }

    private static string BuildUserPrompt(NormalizedMessage message, string text, DateTimeOffset sentAtLocal)
    {
        return $$"""
            Message metadata:
            - source: {{message.Source}}
            - sender_name: {{message.SenderName}}
            - sent_at_utc: {{message.SentAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)}}
            - sent_at_local: {{sentAtLocal.ToString("O", CultureInfo.InvariantCulture)}}

            Message text:
            {{text}}
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
