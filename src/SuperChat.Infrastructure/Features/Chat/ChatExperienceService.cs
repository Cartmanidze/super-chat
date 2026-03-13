using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SuperChat.Contracts.Configuration;
using SuperChat.Contracts.ViewModels;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

public sealed class ChatExperienceService(
    IDigestService digestService,
    IRetrievalService retrievalService,
    ISearchService searchService,
    IMessageNormalizationService messageNormalizationService,
    IRoomDisplayNameService roomDisplayNameService,
    TimeProvider timeProvider,
    PilotOptions pilotOptions,
    ILogger<ChatExperienceService> logger) : IChatExperienceService
{
    private static readonly HashSet<string> StopWords =
    [
        "a", "an", "and", "are", "for", "from", "how", "that", "the", "this", "what", "with",
        "about", "where", "when", "your", "have", "been", "will",
        "как", "какие", "какой", "мне", "мой", "моя", "мои", "надо", "нужно", "что", "это",
        "этот", "эта", "эти", "там", "тут", "где", "когда", "или", "для", "про", "могу",
        "есть", "было", "были", "если", "мое", "меня", "него", "нее"
    ];

    public async Task<ChatAnswerViewModel> AskAsync(Guid userId, ChatPromptRequest request, CancellationToken cancellationToken)
    {
        var templateId = ChatPromptTemplate.Normalize(request.TemplateId);
        if (!ChatPromptTemplate.IsSupported(templateId))
        {
            throw new ArgumentException("Unsupported chat template.", nameof(request.TemplateId));
        }

        var question = request.Question.Trim();
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Question is required.", nameof(request.Question));
        }

        if (question.Length > ChatPromptRequest.MaxQuestionLength)
        {
            throw new ArgumentException($"Question must be {ChatPromptRequest.MaxQuestionLength} characters or fewer.", nameof(request.Question));
        }

        return templateId switch
        {
            ChatPromptTemplate.Today => await BuildTodayAnswerAsync(userId, question, cancellationToken),
            ChatPromptTemplate.Waiting => await BuildWaitingAnswerAsync(userId, question, cancellationToken),
            ChatPromptTemplate.Meetings => await BuildMeetingsAnswerAsync(userId, question, cancellationToken),
            ChatPromptTemplate.Recent => await BuildRecentAnswerAsync(userId, question, cancellationToken),
            ChatPromptTemplate.Custom => await BuildCustomAnswerAsync(userId, question, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(request.TemplateId))
        };
    }

    private async Task<ChatAnswerViewModel> BuildTodayAnswerAsync(Guid userId, string question, CancellationToken cancellationToken)
    {
        var cards = await digestService.GetTodayAsync(userId, cancellationToken);
        return new ChatAnswerViewModel(
            ChatPromptTemplate.Today,
            question,
            cards.Select(card => new ChatResultItemViewModel(
                card.Title,
                card.Summary,
                card.SourceRoom,
                card.DueAt))
            .ToList());
    }

    private async Task<ChatAnswerViewModel> BuildWaitingAnswerAsync(Guid userId, string question, CancellationToken cancellationToken)
    {
        var cards = await digestService.GetWaitingAsync(userId, cancellationToken);
        return new ChatAnswerViewModel(
            ChatPromptTemplate.Waiting,
            question,
            cards.Select(card => MapCard(card, "Awaiting response"))
            .ToList());
    }

    private async Task<ChatAnswerViewModel> BuildMeetingsAnswerAsync(Guid userId, string question, CancellationToken cancellationToken)
    {
        var cards = await digestService.GetMeetingsAsync(userId, cancellationToken);
        return new ChatAnswerViewModel(
            ChatPromptTemplate.Meetings,
            question,
            cards.Select(card => MapCard(card, "Upcoming meeting"))
            .ToList());
    }

    private async Task<ChatAnswerViewModel> BuildRecentAnswerAsync(Guid userId, string question, CancellationToken cancellationToken)
    {
        var messages = await messageNormalizationService.GetRecentMessagesAsync(userId, 80, cancellationToken);
        var timeZone = ResolveTodayTimeZone(logger, pilotOptions.TodayTimeZoneId);
        var now = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone);
        var dayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        var nextDayStart = dayStart.AddDays(1);

        var recentToday = messages
            .Where(message =>
            {
                var localSentAt = TimeZoneInfo.ConvertTime(message.SentAt, timeZone);
                return localSentAt >= dayStart && localSentAt < nextDayStart;
            })
            .Take(8)
            .ToList();

        var roomNames = await roomDisplayNameService.ResolveManyAsync(userId, recentToday.Select(message => message.MatrixRoomId), cancellationToken);
        var items = recentToday
            .Select(message =>
            {
                var sourceRoom = roomNames.TryGetValue(message.MatrixRoomId, out var roomName)
                    ? roomName
                    : LooksLikeMatrixRoomId(message.MatrixRoomId)
                        ? string.Empty
                        : message.MatrixRoomId;

                return new ChatResultItemViewModel(
                    MessagePresentationFormatter.ResolveDisplaySenderName(message.SenderName, sourceRoom),
                    message.Text,
                    sourceRoom,
                    message.SentAt);
            })
            .ToList();

        return new ChatAnswerViewModel(ChatPromptTemplate.Recent, question, items);
    }

    private async Task<ChatAnswerViewModel> BuildCustomAnswerAsync(Guid userId, string question, CancellationToken cancellationToken)
    {
        var routedTemplate = DetectTemplateFromQuestion(question);
        if (routedTemplate is not null)
        {
            return routedTemplate switch
            {
                ChatPromptTemplate.Today => await BuildTodayAnswerAsync(userId, question, cancellationToken),
                ChatPromptTemplate.Waiting => await BuildWaitingAnswerAsync(userId, question, cancellationToken),
                ChatPromptTemplate.Meetings => await BuildMeetingsAnswerAsync(userId, question, cancellationToken),
                ChatPromptTemplate.Recent => await BuildRecentAnswerAsync(userId, question, cancellationToken),
                _ => throw new InvalidOperationException("Unsupported routed chat template.")
            };
        }

        var retrievalResults = await RetrieveSmartAsync(userId, question, cancellationToken);
        if (retrievalResults.Count > 0)
        {
            return new ChatAnswerViewModel(
                ChatPromptTemplate.Custom,
                question,
                retrievalResults.Select(result => new ChatResultItemViewModel(
                    result.Title,
                    result.Summary,
                    result.SourceRoom,
                    result.ObservedAt))
                .ToList());
        }

        var results = await SearchSmartAsync(userId, question, cancellationToken);
        return new ChatAnswerViewModel(
            ChatPromptTemplate.Custom,
            question,
            results.Select(result => new ChatResultItemViewModel(
                result.Title,
                result.Summary,
                result.SourceRoom,
                result.ObservedAt))
            .ToList());
    }

    private async Task<IReadOnlyList<SearchResultViewModel>> RetrieveSmartAsync(
        Guid userId,
        string question,
        CancellationToken cancellationToken)
    {
        try
        {
            var retrievedChunks = await retrievalService.RetrieveAsync(
                new RetrievalRequest(userId, question, "chat_custom"),
                cancellationToken);

            if (retrievedChunks.Count == 0)
            {
                return [];
            }

            var roomNames = await roomDisplayNameService.ResolveManyAsync(
                userId,
                retrievedChunks.Select(item => item.ChatId),
                cancellationToken);

            return retrievedChunks
                .Select(item =>
                {
                    var sourceRoom = roomNames.TryGetValue(item.ChatId, out var roomName)
                        ? roomName
                        : string.Empty;

                    return new SearchResultViewModel(
                        BuildRetrievedTitle(item.Text),
                        BuildRetrievedSummary(item.Text),
                        item.Kind,
                        sourceRoom,
                        item.TsTo);
                })
                .ToList();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Retrieval pipeline failed for user {UserId}; falling back to token search.", userId);
            return [];
        }
    }

    private async Task<IReadOnlyList<SearchResultViewModel>> SearchSmartAsync(
        Guid userId,
        string question,
        CancellationToken cancellationToken)
    {
        var directResults = await searchService.SearchAsync(userId, question, cancellationToken);
        if (directResults.Count > 0)
        {
            return directResults;
        }

        var deduplicated = new List<SearchResultViewModel>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidate in BuildSearchCandidates(question))
        {
            var candidateResults = await searchService.SearchAsync(userId, candidate, cancellationToken);
            foreach (var result in candidateResults)
            {
                var key = $"{result.Title}|{result.Summary}|{result.SourceRoom}|{result.ObservedAt:O}";
                if (seen.Add(key))
                {
                    deduplicated.Add(result);
                }
            }

            if (deduplicated.Count >= 8)
            {
                break;
            }
        }

        return deduplicated
            .OrderByDescending(result => result.ObservedAt)
            .Take(8)
            .ToList();
    }

    private static IEnumerable<string> BuildSearchCandidates(string question)
    {
        return Regex.Matches(question.ToLowerInvariant(), @"[\p{L}\p{Nd}_-]+")
            .Select(match => match.Value)
            .Where(token => token.Length >= 3)
            .Where(token => !StopWords.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .Take(6)
            .ToList();
    }

    internal static string BuildRetrievedTitle(string text)
    {
        var firstLine = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return "Relevant context";
        }

        return firstLine.Length <= 72
            ? firstLine
            : $"{firstLine[..69]}...";
    }

    internal static string BuildRetrievedSummary(string text)
    {
        var normalized = text.Trim();
        if (normalized.Length <= 320)
        {
            return normalized;
        }

        return $"{normalized[..317]}...";
    }

    private static string? DetectTemplateFromQuestion(string question)
    {
        var normalized = question.ToLowerInvariant();

        if (ContainsAny(normalized, "встреч", "созвон", "meeting", "call", "calendar"))
        {
            return ChatPromptTemplate.Meetings;
        }

        if (ContainsAny(normalized, "жду", "ожида", "waiting", "reply", "ответ"))
        {
            return ChatPromptTemplate.Waiting;
        }

        if (ContainsAny(normalized, "сообщен", "recent", "последн", "что было", "что пришло"))
        {
            return ChatPromptTemplate.Recent;
        }

        if (ContainsAny(normalized, "сегодня", "важно", "important", "today", "приоритет"))
        {
            return ChatPromptTemplate.Today;
        }

        return null;
    }

    private static ChatResultItemViewModel MapCard(DashboardCardViewModel card, string genericTitle)
    {
        if (string.Equals(card.Title, genericTitle, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(card.Summary))
        {
            return new ChatResultItemViewModel(card.Summary, string.Empty, card.SourceRoom, card.DueAt);
        }

        return new ChatResultItemViewModel(card.Title, card.Summary, card.SourceRoom, card.DueAt);
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.Ordinal));
    }

    private static bool LooksLikeMatrixRoomId(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.StartsWith("!", StringComparison.Ordinal) &&
               value.Contains(':', StringComparison.Ordinal);
    }

    private static TimeZoneInfo ResolveTodayTimeZone(ILogger logger, string configuredTimeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(configuredTimeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(configuredTimeZoneId);
            }
            catch (TimeZoneNotFoundException ex)
            {
                logger.LogWarning(ex, "Configured chat time zone '{TimeZoneId}' was not found. Falling back to UTC.", configuredTimeZoneId);
            }
            catch (InvalidTimeZoneException ex)
            {
                logger.LogWarning(ex, "Configured chat time zone '{TimeZoneId}' is invalid. Falling back to UTC.", configuredTimeZoneId);
            }
        }

        return TimeZoneInfo.Utc;
    }
}
