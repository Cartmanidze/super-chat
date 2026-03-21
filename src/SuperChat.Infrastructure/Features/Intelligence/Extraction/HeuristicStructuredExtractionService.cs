using System.Globalization;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Domain.Features.Messaging;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Features.Intelligence.Extraction;

public sealed class HeuristicStructuredExtractionService(
    PilotOptions pilotOptions,
    ITextEnrichmentClient textEnrichmentClient)
{
    private static readonly string[] MeetingCueKeywords =
    [
        "meeting",
        "call",
        "sync",
        "calendar",
        "zoom",
        "встреч",
        "созвон",
        "колл",
        "зум",
        "календар"
    ];

    private const string RussianInterviewStem = "\u0441\u043e\u0431\u0435\u0441\u0435\u0434";
    private const int MaxMessageLevelRecoveryAttempts = 4;

    public async Task<IReadOnlyCollection<ExtractedItem>> ExtractAsync(ConversationWindow window, CancellationToken cancellationToken)
    {
        var referenceTimeZone = ResolveReferenceTimeZone(pilotOptions.TodayTimeZoneId);
        var items = HeuristicSignalDetector.Detect(window, referenceTimeZone).ToList();
        if (items.Count == 0)
        {
            var recoveredItems = await RecoverMeetingFromTemporalEnrichmentAsync(
                window,
                referenceTimeZone,
                cancellationToken);
            if (recoveredItems.Count == 0)
            {
                return recoveredItems;
            }

            items = recoveredItems.ToList();
        }

        await EnrichItemsAsync(window, items, referenceTimeZone, cancellationToken);
        ApplyWaitingOnWindowRules(window, items);
        return items;
    }

    public Task<IReadOnlyCollection<ExtractedItem>> ExtractAsync(NormalizedMessage message, CancellationToken cancellationToken)
    {
        var window = new ConversationWindow(
            message.UserId,
            message.Source,
            message.MatrixRoomId,
            [message]);

        return ExtractAsync(window, cancellationToken);
    }

    public static Task<IReadOnlyCollection<ExtractedItem>> ExtractCoreAsync(
        NormalizedMessage message,
        TimeZoneInfo referenceTimeZone,
        CancellationToken cancellationToken)
    {
        var window = new ConversationWindow(
            message.UserId,
            message.Source,
            message.MatrixRoomId,
            [message]);

        return Task.FromResult(HeuristicSignalDetector.Detect(window, referenceTimeZone));
    }

    private async Task EnrichItemsAsync(
        ConversationWindow window,
        List<ExtractedItem> items,
        TimeZoneInfo referenceTimeZone,
        CancellationToken cancellationToken)
    {
        if (!textEnrichmentClient.IsConfigured)
        {
            return;
        }

        var messagesByEventId = window.Messages
            .GroupBy(message => message.MatrixEventId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

        var enrichmentByEventId = new Dictionary<string, (TextEnrichmentResponse Enrichment, NormalizedMessage Message)>(StringComparer.Ordinal);

        foreach (var sourceEventId in items
                     .Select(item => item.SourceEventId)
                     .Where(eventId => !string.IsNullOrWhiteSpace(eventId))
                     .Distinct(StringComparer.Ordinal))
        {
            if (!messagesByEventId.TryGetValue(sourceEventId, out var message))
            {
                continue;
            }

            var enrichment = await textEnrichmentClient.EnrichAsync(
                message.Text,
                message.SentAt,
                referenceTimeZone.Id,
                cancellationToken);

            if (enrichment is not null)
            {
                enrichmentByEventId[sourceEventId] = (enrichment, message);
            }
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (!enrichmentByEventId.TryGetValue(item.SourceEventId, out var enrichmentEntry))
            {
                continue;
            }

            var enrichment = enrichmentEntry.Enrichment;
            var person = item.Person;
            if (string.IsNullOrWhiteSpace(person))
            {
                person = ResolveCounterpartyName(enrichment);
            }

            var title = item.Kind == ExtractedItemKind.WaitingOn &&
                        string.Equals(item.Title, "Нужно ответить", StringComparison.Ordinal) &&
                        !string.IsNullOrWhiteSpace(person)
                ? $"Нужно ответить: {person}"
                : item.Title;

            var dueAt = item.DueAt ?? ResolveDueAt(
                enrichment,
                referenceTimeZone,
                enrichmentEntry.Message.SentAt,
                requireFuture: item.Kind == ExtractedItemKind.Meeting);
            items[index] = item with
            {
                Title = title,
                Person = person,
                DueAt = dueAt
            };
        }
    }

    private async Task<IReadOnlyCollection<ExtractedItem>> RecoverMeetingFromTemporalEnrichmentAsync(
        ConversationWindow window,
        TimeZoneInfo referenceTimeZone,
        CancellationToken cancellationToken)
    {
        if (!textEnrichmentClient.IsConfigured || window.Messages.Count == 0)
        {
            return Array.Empty<ExtractedItem>();
        }

        var transcript = window.Transcript.Trim();
        if (string.IsNullOrWhiteSpace(transcript) || StructuredArtifactDetector.LooksLikeStructuredArtifact(transcript))
        {
            return Array.Empty<ExtractedItem>();
        }

        var messageLevelItem = await TryRecoverMeetingFromMessageLevelEnrichmentAsync(
            window,
            referenceTimeZone,
            cancellationToken);
        if (messageLevelItem is not null)
        {
            return [messageLevelItem];
        }

        if (!IsMeetingCueText(transcript))
        {
            return Array.Empty<ExtractedItem>();
        }

        var enrichment = await textEnrichmentClient.EnrichAsync(
            transcript,
            window.TsTo,
            referenceTimeZone.Id,
            cancellationToken);
        if (enrichment is null)
        {
            return Array.Empty<ExtractedItem>();
        }

        var dueAt = ResolveDueAt(
            enrichment,
            referenceTimeZone,
            window.TsTo,
            requireFuture: true);
        if (dueAt is null)
        {
            return Array.Empty<ExtractedItem>();
        }

        var sourceMessage = window.Messages
            .LastOrDefault(message => IsMeetingCueText(message.Text)) ?? window.LastMessage;
        var summary = string.IsNullOrWhiteSpace(sourceMessage.Text)
            ? transcript
            : sourceMessage.Text.Trim();
        var person = ResolveCounterpartyName(enrichment);

        return [CreateRecoveredMeetingItem(sourceMessage, summary, person, dueAt.Value, 0.62)];
    }

    private async Task<ExtractedItem?> TryRecoverMeetingFromMessageLevelEnrichmentAsync(
        ConversationWindow window,
        TimeZoneInfo referenceTimeZone,
        CancellationToken cancellationToken)
    {
        var candidateMessages = window.Messages
            .Where(message => !string.IsNullOrWhiteSpace(message.Text))
            .Where(message => !StructuredArtifactDetector.LooksLikeStructuredArtifact(message.Text))
            .Where(message => IsMeetingCueText(message.Text))
            .Reverse()
            .Take(MaxMessageLevelRecoveryAttempts);

        foreach (var message in candidateMessages)
        {
            var enrichment = await textEnrichmentClient.EnrichAsync(
                message.Text,
                message.SentAt,
                referenceTimeZone.Id,
                cancellationToken);
            if (enrichment is null)
            {
                continue;
            }

            var dueAt = ResolveDueAt(
                enrichment,
                referenceTimeZone,
                message.SentAt,
                requireFuture: true);
            if (dueAt is null)
            {
                continue;
            }

            var summary = message.Text.Trim();
            var person = ResolveCounterpartyName(enrichment);
            return CreateRecoveredMeetingItem(message, summary, person, dueAt.Value, 0.68);
        }

        return null;
    }

    internal static void ApplyWaitingOnWindowRules(ConversationWindow window, List<ExtractedItem> items)
    {
        var unresolvedMessage = WaitingOnTurnDetector.GetUnansweredExternalMessage(window);
        if (unresolvedMessage is null)
        {
            items.RemoveAll(item => item.Kind == ExtractedItemKind.WaitingOn);
            return;
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item.Kind != ExtractedItemKind.WaitingOn)
            {
                continue;
            }

            var person = string.IsNullOrWhiteSpace(item.Person) && CanUseCounterpartyName(unresolvedMessage.SenderName)
                ? unresolvedMessage.SenderName.Trim()
                : item.Person;

            var title = string.Equals(item.Title, "Нужно ответить", StringComparison.Ordinal) &&
                        !string.IsNullOrWhiteSpace(person)
                ? $"Нужно ответить: {person}"
                : item.Title;

            items[index] = item with
            {
                Title = title,
                Person = person,
                SourceEventId = unresolvedMessage.MatrixEventId,
                ObservedAt = unresolvedMessage.SentAt
            };
        }
    }

    private static ExtractedItem CreateRecoveredMeetingItem(
        NormalizedMessage sourceMessage,
        string summary,
        string? person,
        DateTimeOffset dueAt,
        double confidence)
    {
        return new ExtractedItem(
            Guid.NewGuid(),
            sourceMessage.UserId,
            ExtractedItemKind.Meeting,
            "\u0421\u043a\u043e\u0440\u043e \u0432\u0441\u0442\u0440\u0435\u0447\u0430",
            summary,
            sourceMessage.MatrixRoomId,
            sourceMessage.MatrixEventId,
            person,
            sourceMessage.SentAt,
            dueAt,
            confidence);
    }

    private static bool IsMeetingCueText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var lowered = text.ToLowerInvariant();
        return lowered.Contains("interview", StringComparison.Ordinal) ||
               lowered.Contains(RussianInterviewStem, StringComparison.Ordinal) ||
               ContainsAny(lowered, MeetingCueKeywords);
    }

    private static bool CanUseCounterpartyName(string senderName)
    {
        return !string.IsNullOrWhiteSpace(senderName) &&
               !string.Equals(senderName.Trim(), "Unknown", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(senderName.Trim(), "You", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveCounterpartyName(TextEnrichmentResponse enrichment)
    {
        if (!string.IsNullOrWhiteSpace(enrichment.CounterpartyName))
        {
            return enrichment.CounterpartyName;
        }

        if (!string.IsNullOrWhiteSpace(enrichment.OrganizationName))
        {
            return enrichment.OrganizationName;
        }

        return enrichment.Entities
            .FirstOrDefault(entity => string.Equals(entity.Type, "PERSON", StringComparison.OrdinalIgnoreCase))?.NormalizedText
            ?? enrichment.Entities
                .FirstOrDefault(entity => string.Equals(entity.Type, "PERSON", StringComparison.OrdinalIgnoreCase))?.Text
            ?? enrichment.Entities
                .FirstOrDefault(entity => string.Equals(entity.Type, "ORG", StringComparison.OrdinalIgnoreCase))?.NormalizedText
            ?? enrichment.Entities
                .FirstOrDefault(entity => string.Equals(entity.Type, "ORG", StringComparison.OrdinalIgnoreCase))?.Text;
    }

    private static DateTimeOffset? ResolveDueAt(
        TextEnrichmentResponse enrichment,
        TimeZoneInfo referenceTimeZone,
        DateTimeOffset referenceTimeUtc,
        bool requireFuture)
    {
        var parsedCandidates = new List<(DateTimeOffset DueAt, int Score)>();

        foreach (var temporalExpression in enrichment.TemporalExpressions)
        {
            if (!TryParseTemporalValue(temporalExpression, referenceTimeZone, out var dueAt, out var score))
            {
                continue;
            }

            parsedCandidates.Add((dueAt, score));
        }

        if (parsedCandidates.Count == 0)
        {
            return null;
        }

        var referenceUtc = referenceTimeUtc.ToUniversalTime();
        var futureCandidates = parsedCandidates
            .Where(candidate => candidate.DueAt >= referenceUtc.AddMinutes(-15))
            .OrderBy(candidate => candidate.DueAt)
            .ThenByDescending(candidate => candidate.Score)
            .ToList();

        if (futureCandidates.Count > 0)
        {
            return futureCandidates[0].DueAt;
        }

        if (requireFuture)
        {
            return null;
        }

        return parsedCandidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.DueAt)
            .First()
            .DueAt;
    }

    private static bool TryParseTemporalValue(
        TextEnrichmentTemporalExpression temporalExpression,
        TimeZoneInfo referenceTimeZone,
        out DateTimeOffset parsedDueAt,
        out int score)
    {
        parsedDueAt = default;
        score = 0;

        var normalizedValue = NormalizeTemporalValue(temporalExpression.Value);
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            return false;
        }

        if (normalizedValue.Contains("UNDEF", StringComparison.OrdinalIgnoreCase) ||
            normalizedValue.EndsWith("_REF", StringComparison.OrdinalIgnoreCase) ||
            normalizedValue.StartsWith("P", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var grain = temporalExpression.Grain?.Trim();
        if (!string.IsNullOrWhiteSpace(grain))
        {
            if (grain.Contains("duration", StringComparison.OrdinalIgnoreCase) ||
                grain.Contains("set", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (grain.Contains("datetime", StringComparison.OrdinalIgnoreCase) ||
                grain.Contains("time", StringComparison.OrdinalIgnoreCase))
            {
                score += 2;
            }
            else if (grain.Contains("date", StringComparison.OrdinalIgnoreCase))
            {
                score++;
            }
        }

        if (DateOnly.TryParseExact(normalizedValue, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
        {
            var localDateTime = new DateTime(dateOnly.Year, dateOnly.Month, dateOnly.Day, 10, 0, 0, DateTimeKind.Unspecified);
            parsedDueAt = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localDateTime, referenceTimeZone), TimeSpan.Zero);
            score++;
            return true;
        }

        var hasExplicitOffset = HasExplicitOffset(normalizedValue);
        if (hasExplicitOffset &&
            DateTimeOffset.TryParse(
                normalizedValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsedOffset))
        {
            parsedDueAt = parsedOffset.ToUniversalTime();
            score += 3;
            return true;
        }

        if (!DateTime.TryParse(
                normalizedValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsedDateTime))
        {
            return false;
        }

        var unspecifiedLocal = DateTime.SpecifyKind(parsedDateTime, DateTimeKind.Unspecified);
        parsedDueAt = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecifiedLocal, referenceTimeZone), TimeSpan.Zero);
        score += normalizedValue.Contains('T', StringComparison.Ordinal) ? 2 : 1;
        return true;
    }

    private static string? NormalizeTemporalValue(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var normalized = rawValue.Trim();
        var rangeSeparatorIndex = normalized.IndexOf('/');
        if (rangeSeparatorIndex > 0)
        {
            normalized = normalized[..rangeSeparatorIndex];
        }

        if (normalized.Length < 8 || normalized.Contains('W', StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return normalized;
    }

    private static bool HasExplicitOffset(string value)
    {
        if (value.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var dateTimeSeparatorIndex = value.IndexOf('T', StringComparison.Ordinal);
        if (dateTimeSeparatorIndex < 0)
        {
            return false;
        }

        var plusOffsetIndex = value.LastIndexOf('+');
        var minusOffsetIndex = value.LastIndexOf('-');
        var offsetIndex = Math.Max(plusOffsetIndex, minusOffsetIndex);
        return offsetIndex > dateTimeSeparatorIndex + 1;
    }

    private static bool ContainsAny(string text, IEnumerable<string> values)
    {
        return values.Any(value => text.Contains(value, StringComparison.Ordinal));
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
