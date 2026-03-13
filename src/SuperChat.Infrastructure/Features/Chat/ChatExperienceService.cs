using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SuperChat.Contracts.ViewModels;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

public sealed class ChatExperienceService : IChatExperienceService
{
    private static readonly HashSet<string> StopWords =
    [
        "a", "an", "and", "are", "for", "from", "how", "that", "the", "this", "what", "with",
        "about", "where", "when", "your", "have", "been", "will",
        "как", "какие", "какой", "мне", "мой", "моя", "мои", "надо", "нужно", "что", "это",
        "этот", "эта", "эти", "там", "тут", "где", "когда", "или", "для", "про", "могу",
        "есть", "было", "были", "если", "мое", "меня", "него", "нее"
    ];

    private readonly IChatTemplateCatalog _templateCatalog;
    private readonly IReadOnlyDictionary<string, IChatTemplateHandler> _handlersById;
    private readonly IRetrievalService _retrievalService;
    private readonly ISearchService _searchService;
    private readonly IRoomDisplayNameService _roomDisplayNameService;
    private readonly ILogger<ChatExperienceService> _logger;

    public ChatExperienceService(
        IChatTemplateCatalog templateCatalog,
        IEnumerable<IChatTemplateHandler> handlers,
        IRetrievalService retrievalService,
        ISearchService searchService,
        IRoomDisplayNameService roomDisplayNameService,
        ILogger<ChatExperienceService> logger)
    {
        _templateCatalog = templateCatalog;
        _handlersById = handlers.ToDictionary(handler => handler.TemplateId, StringComparer.OrdinalIgnoreCase);
        _retrievalService = retrievalService;
        _searchService = searchService;
        _roomDisplayNameService = roomDisplayNameService;
        _logger = logger;
    }

    public Task<ChatAnswerViewModel> AskAsync(Guid userId, ChatPromptRequest request, CancellationToken cancellationToken)
    {
        var templateId = ChatPromptTemplate.Normalize(request.TemplateId);
        if (!_templateCatalog.TryGetTemplate(templateId, out _))
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

        return ChatPromptTemplate.IsCustom(templateId)
            ? BuildCustomAnswerAsync(userId, question, cancellationToken)
            : DispatchToTemplateAsync(userId, templateId, question, cancellationToken);
    }

    private Task<ChatAnswerViewModel> DispatchToTemplateAsync(Guid userId, string templateId, string question, CancellationToken cancellationToken)
    {
        if (_handlersById.TryGetValue(templateId, out var handler))
        {
            return handler.HandleAsync(userId, question, cancellationToken);
        }

        throw new InvalidOperationException($"No chat template handler is registered for '{templateId}'.");
    }

    private async Task<ChatAnswerViewModel> BuildCustomAnswerAsync(Guid userId, string question, CancellationToken cancellationToken)
    {
        var routedTemplate = DetectTemplateFromQuestion(question);
        if (routedTemplate is not null)
        {
            return await DispatchToTemplateAsync(userId, routedTemplate, question, cancellationToken);
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
            var retrievedChunks = await _retrievalService.RetrieveAsync(
                new RetrievalRequest(userId, question, "chat_custom"),
                cancellationToken);

            if (retrievedChunks.Count == 0)
            {
                return [];
            }

            var roomNames = await _roomDisplayNameService.ResolveManyAsync(
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
            _logger.LogWarning(exception, "Retrieval pipeline failed for user {UserId}; falling back to token search.", userId);
            return [];
        }
    }

    private async Task<IReadOnlyList<SearchResultViewModel>> SearchSmartAsync(
        Guid userId,
        string question,
        CancellationToken cancellationToken)
    {
        var directResults = await _searchService.SearchAsync(userId, question, cancellationToken);
        if (directResults.Count > 0)
        {
            return directResults;
        }

        var deduplicated = new List<SearchResultViewModel>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidate in BuildSearchCandidates(question))
        {
            var candidateResults = await _searchService.SearchAsync(userId, candidate, cancellationToken);
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

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.Ordinal));
    }
}
