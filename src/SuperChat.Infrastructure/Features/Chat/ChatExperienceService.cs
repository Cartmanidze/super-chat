using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SuperChat.Contracts.Features.Chat;
using SuperChat.Contracts.Features.Search;
using SuperChat.Domain.Features.Chat;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Diagnostics;
using SuperChat.Infrastructure.Features.Search;

namespace SuperChat.Infrastructure.Features.Chat;

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
    private readonly IChatAnswerGenerationService _chatAnswerGenerationService;
    private readonly IRetrievalService _retrievalService;
    private readonly ISearchService _searchService;
    private readonly IRoomDisplayNameService _roomDisplayNameService;
    private readonly ILogger<ChatExperienceService> _logger;

    public ChatExperienceService(
        IChatTemplateCatalog templateCatalog,
        IEnumerable<IChatTemplateHandler> handlers,
        IChatAnswerGenerationService chatAnswerGenerationService,
        IRetrievalService retrievalService,
        ISearchService searchService,
        IRoomDisplayNameService roomDisplayNameService,
        ILogger<ChatExperienceService> logger)
    {
        _templateCatalog = templateCatalog;
        _handlersById = handlers.ToDictionary(handler => handler.TemplateId, StringComparer.OrdinalIgnoreCase);
        _chatAnswerGenerationService = chatAnswerGenerationService;
        _retrievalService = retrievalService;
        _searchService = searchService;
        _roomDisplayNameService = roomDisplayNameService;
        _logger = logger;
    }

    public async Task<ChatAnswerViewModel> AskAsync(Guid userId, ChatPromptRequest request, CancellationToken cancellationToken)
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

        var stopwatch = Stopwatch.StartNew();

        using (_logger.BeginScope(new Dictionary<string, object?>
               {
                   ["user_id"] = userId,
                   ["template_id"] = templateId
               }))
        {
            AiPipelineLog.ChatPipelineStarted(_logger, templateId, question.Length);

            try
            {
                var answer = await (ChatPromptTemplate.IsCustom(templateId)
                    ? BuildCustomAnswerAsync(userId, question, cancellationToken)
                    : BuildTemplateAnswerAsync(userId, templateId, question, cancellationToken));

                stopwatch.Stop();
                AiPipelineLog.ChatPipelineCompleted(
                    _logger,
                    templateId,
                    stopwatch.ElapsedMilliseconds,
                    answer.Items.Count,
                    answer.AssistantText?.Length ?? 0);

                return answer;
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                AiPipelineLog.ChatPipelineFailed(_logger, templateId, stopwatch.ElapsedMilliseconds, exception);
                throw;
            }
        }
    }

    private Task<ChatAnswerViewModel> DispatchToTemplateAsync(Guid userId, string templateId, string question, CancellationToken cancellationToken)
    {
        if (_handlersById.TryGetValue(templateId, out var handler))
        {
            return handler.HandleAsync(userId, question, cancellationToken);
        }

        throw new InvalidOperationException($"No chat template handler is registered for '{templateId}'.");
    }

    private async Task<ChatAnswerViewModel> BuildTemplateAnswerAsync(Guid userId, string templateId, string question, CancellationToken cancellationToken)
    {
        var baseAnswer = await DispatchToTemplateAsync(userId, templateId, question, cancellationToken);
        var generatedAnswer = await TryEnhanceTemplateAnswerAsync(baseAnswer, cancellationToken);
        return generatedAnswer ?? baseAnswer;
    }

    private async Task<ChatAnswerViewModel> BuildCustomAnswerAsync(Guid userId, string question, CancellationToken cancellationToken)
    {
        var routedTemplate = DetectTemplateFromQuestion(question);
        if (routedTemplate is not null)
        {
            AiPipelineLog.CustomQuestionRoutedToTemplate(_logger, routedTemplate);
            return await DispatchToTemplateAsync(userId, routedTemplate, question, cancellationToken);
        }

        var retrievalResults = await RetrieveSmartAsync(userId, question, cancellationToken);
        AiPipelineLog.CustomRetrievalCompleted(_logger, retrievalResults.Count);
        if (retrievalResults.Count > 0)
        {
            var generatedAnswer = await _chatAnswerGenerationService.TryGenerateAsync(
                question,
                retrievalResults.Select((result, index) => new ChatAnswerContextItem(
                    $"ctx_{index + 1}",
                    result.SourceRoom,
                    result.ObservedAt,
                    result.Text))
                .ToList(),
                cancellationToken);

            if (generatedAnswer is not null)
            {
                var contextsByReference = retrievalResults
                    .Select((result, index) => new { ReferenceKey = $"ctx_{index + 1}", Result = result })
                    .ToDictionary(item => item.ReferenceKey, item => item.Result, StringComparer.Ordinal);

                var answerItems = generatedAnswer.Items
                    .Where(item => contextsByReference.ContainsKey(item.ReferenceKey))
                    .Select(item =>
                    {
                        var context = contextsByReference[item.ReferenceKey];
                        return item.ToChatResultItemViewModel(context);
                    })
                    .ToList();

                if (answerItems.Count > 0 || !string.IsNullOrWhiteSpace(generatedAnswer.AssistantText))
                {
                    return answerItems.ToChatAnswerViewModel(
                        ChatPromptTemplate.Custom,
                        question,
                        generatedAnswer.AssistantText);
                }
            }

            return retrievalResults
                .Select(result => result.ToChatResultItemViewModel())
                .ToChatAnswerViewModel(ChatPromptTemplate.Custom, question);
        }

        var results = await SearchSmartAsync(userId, question, cancellationToken);
        AiPipelineLog.CustomSearchFallbackCompleted(_logger, results.Count);
        return results
            .Select(result => result.ToChatResultItemViewModel())
            .ToChatAnswerViewModel(ChatPromptTemplate.Custom, question);
    }

    private async Task<ChatAnswerViewModel?> TryEnhanceTemplateAnswerAsync(
        ChatAnswerViewModel baseAnswer,
        CancellationToken cancellationToken)
    {
        var contextItems = baseAnswer.Items
            .Select((item, index) => new
            {
                ReferenceKey = $"ctx_{index + 1}",
                ViewItem = item,
                ContextText = BuildContextText(item)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.ContextText))
            .Select(item => new
            {
                item.ReferenceKey,
                item.ViewItem,
                ContextItem = new ChatAnswerContextItem(
                    item.ReferenceKey,
                    item.ViewItem.SourceRoom,
                    item.ViewItem.Timestamp,
                    item.ContextText)
            })
            .ToList();

        if (contextItems.Count == 0)
        {
            return null;
        }

        var stopwatch = Stopwatch.StartNew();
        AiPipelineLog.TemplateAnswerEnhancementStarted(_logger, baseAnswer.Mode, contextItems.Count);

        var generatedAnswer = await _chatAnswerGenerationService.TryGenerateAsync(
            baseAnswer.Question,
            contextItems.Select(item => item.ContextItem).ToList(),
            cancellationToken);

        if (generatedAnswer is null)
        {
            stopwatch.Stop();
            AiPipelineLog.TemplateAnswerEnhancementCompleted(_logger, baseAnswer.Mode, contextItems.Count, 0, 0, stopwatch.ElapsedMilliseconds);
            return null;
        }

        var itemsByReference = contextItems.ToDictionary(
            item => item.ReferenceKey,
            item => item.ViewItem,
            StringComparer.Ordinal);

        var generatedItems = generatedAnswer.Items
            .Where(item => itemsByReference.ContainsKey(item.ReferenceKey))
            .Select(item =>
            {
                var sourceItem = itemsByReference[item.ReferenceKey];
                return item.ToChatResultItemViewModel(sourceItem);
            })
            .DistinctBy(item => $"{item.Title}|{item.Summary}|{item.SourceRoom}|{item.Timestamp:O}", StringComparer.Ordinal)
            .ToList();

        if (string.IsNullOrWhiteSpace(generatedAnswer.AssistantText) && generatedItems.Count == 0)
        {
            stopwatch.Stop();
            AiPipelineLog.TemplateAnswerEnhancementCompleted(_logger, baseAnswer.Mode, contextItems.Count, 0, 0, stopwatch.ElapsedMilliseconds);
            return null;
        }

        if (generatedItems.Count == 0 &&
            LooksLikeNoContextAnswer(generatedAnswer.AssistantText))
        {
            stopwatch.Stop();
            AiPipelineLog.TemplateAnswerEnhancementCompleted(
                _logger,
                baseAnswer.Mode,
                contextItems.Count,
                0,
                generatedAnswer.AssistantText?.Length ?? 0,
                stopwatch.ElapsedMilliseconds);

            return Array.Empty<ChatResultItemViewModel>()
                .ToChatAnswerViewModel(baseAnswer.Mode, baseAnswer.Question, generatedAnswer.AssistantText);
        }

        stopwatch.Stop();
        AiPipelineLog.TemplateAnswerEnhancementCompleted(
            _logger,
            baseAnswer.Mode,
            contextItems.Count,
            generatedItems.Count,
            generatedAnswer.AssistantText?.Length ?? 0,
            stopwatch.ElapsedMilliseconds);

        return (generatedItems.Count > 0 ? generatedItems : baseAnswer.Items)
            .ToChatAnswerViewModel(
                baseAnswer.Mode,
                baseAnswer.Question,
                string.IsNullOrWhiteSpace(generatedAnswer.AssistantText)
                    ? baseAnswer.AssistantText
                    : generatedAnswer.AssistantText);
    }

    private static bool LooksLikeNoContextAnswer(string? assistantText)
    {
        if (string.IsNullOrWhiteSpace(assistantText))
        {
            return false;
        }

        var normalized = assistantText.Trim().ToLowerInvariant();
        return ContainsAny(
            normalized,
            "контекст не содержит",
            "не содержит информации",
            "недостаточно информации",
            "нет информации",
            "не удалось найти",
            "не могу определить",
            "предоставленные данные описывают",
            "no relevant context",
            "context does not contain",
            "does not contain information",
            "not enough information",
            "insufficient information",
            "cannot determine",
            "provided data describes");
    }

    private async Task<IReadOnlyList<RetrievedChatContext>> RetrieveSmartAsync(
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

                    return new RetrievedChatContext(
                        BuildRetrievedTitle(item.Text),
                        BuildRetrievedSummary(item.Text),
                        sourceRoom,
                        item.TsTo,
                        item.Text);
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

    private static string BuildContextText(ChatResultItemViewModel item)
    {
        var segments = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(item.Title))
        {
            segments.Add(item.Title.Trim());
        }

        if (!string.IsNullOrWhiteSpace(item.Summary))
        {
            segments.Add(item.Summary.Trim());
        }

        return string.Join(Environment.NewLine, segments);
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
