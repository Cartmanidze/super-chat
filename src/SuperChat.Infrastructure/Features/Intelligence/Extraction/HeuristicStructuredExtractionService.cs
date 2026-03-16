using SuperChat.Contracts.Configuration;
using SuperChat.Domain.Model;
using SuperChat.Domain.Services;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

public sealed class HeuristicStructuredExtractionService(
    PilotOptions pilotOptions,
    ITextEnrichmentClient textEnrichmentClient)
{
    public async Task<IReadOnlyCollection<ExtractedItem>> ExtractAsync(ConversationWindow window, CancellationToken cancellationToken)
    {
        var referenceTimeZone = ResolveReferenceTimeZone(pilotOptions.TodayTimeZoneId);
        var items = HeuristicSignalDetector.Detect(window, referenceTimeZone).ToList();
        if (items.Count == 0)
        {
            return items;
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

        var enrichmentByEventId = new Dictionary<string, TextEnrichmentResponse?>(StringComparer.Ordinal);

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

            enrichmentByEventId[sourceEventId] = enrichment;
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (!enrichmentByEventId.TryGetValue(item.SourceEventId, out var enrichment) || enrichment is null)
            {
                continue;
            }

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

            var dueAt = item.DueAt ?? ResolveDueAt(enrichment);
            items[index] = item with
            {
                Title = title,
                Person = person,
                DueAt = dueAt
            };
        }
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

    private static DateTimeOffset? ResolveDueAt(TextEnrichmentResponse enrichment)
    {
        foreach (var temporalExpression in enrichment.TemporalExpressions)
        {
            if (string.IsNullOrWhiteSpace(temporalExpression.Value))
            {
                continue;
            }

            if (DateTimeOffset.TryParse(
                    temporalExpression.Value,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                    out var parsed))
            {
                return parsed.ToUniversalTime();
            }
        }

        return null;
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
