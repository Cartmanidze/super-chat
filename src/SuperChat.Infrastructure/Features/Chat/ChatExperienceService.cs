using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SuperChat.Contracts.Features.Chat;
using SuperChat.Domain.Features.Chat;
using SuperChat.Infrastructure.Diagnostics;

namespace SuperChat.Infrastructure.Features.Chat;

public sealed class ChatExperienceService : IChatExperienceService
{
    private readonly IChatTemplateCatalog _templateCatalog;
    private readonly IReadOnlyDictionary<string, IChatTemplateHandler> _handlersById;
    private readonly IChatAnswerGenerationService _chatAnswerGenerationService;
    private readonly ILogger<ChatExperienceService> _logger;

    public ChatExperienceService(
        IChatTemplateCatalog templateCatalog,
        IEnumerable<IChatTemplateHandler> handlers,
        IChatAnswerGenerationService chatAnswerGenerationService,
        ILogger<ChatExperienceService> logger)
    {
        _templateCatalog = templateCatalog;
        _handlersById = handlers.ToDictionary(handler => handler.TemplateId, StringComparer.OrdinalIgnoreCase);
        _chatAnswerGenerationService = chatAnswerGenerationService;
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
                var answer = await BuildTemplateAnswerAsync(userId, templateId, question, cancellationToken);

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
                    item.ViewItem.ChatTitle,
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
            .DistinctBy(item => $"{item.Title}|{item.Summary}|{item.ChatTitle}|{item.Timestamp:O}", StringComparer.Ordinal)
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
        return normalized.Contains("контекст не содержит", StringComparison.Ordinal) ||
               normalized.Contains("не содержит информации", StringComparison.Ordinal) ||
               normalized.Contains("недостаточно информации", StringComparison.Ordinal) ||
               normalized.Contains("нет информации", StringComparison.Ordinal) ||
               normalized.Contains("не удалось найти", StringComparison.Ordinal) ||
               normalized.Contains("не могу определить", StringComparison.Ordinal) ||
               normalized.Contains("предоставленные данные описывают", StringComparison.Ordinal) ||
               normalized.Contains("no relevant context", StringComparison.Ordinal) ||
               normalized.Contains("context does not contain", StringComparison.Ordinal) ||
               normalized.Contains("does not contain information", StringComparison.Ordinal) ||
               normalized.Contains("not enough information", StringComparison.Ordinal) ||
               normalized.Contains("insufficient information", StringComparison.Ordinal) ||
               normalized.Contains("cannot determine", StringComparison.Ordinal) ||
               normalized.Contains("provided data describes", StringComparison.Ordinal);
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
}
