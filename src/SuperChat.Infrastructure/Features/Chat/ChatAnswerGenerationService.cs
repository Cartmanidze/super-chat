using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Diagnostics;

namespace SuperChat.Infrastructure.Services;

public sealed class ChatAnswerGenerationService(
    IDeepSeekJsonClient deepSeekJsonClient,
    IOptions<ChatAnsweringOptions> options,
    ILogger<ChatAnswerGenerationService> logger) : IChatAnswerGenerationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GeneratedChatAnswer?> TryGenerateAsync(
        string question,
        IReadOnlyList<ChatAnswerContextItem> contextItems,
        CancellationToken cancellationToken)
    {
        var configuredOptions = options.Value;
        if (!configuredOptions.Enabled || !deepSeekJsonClient.IsConfigured || contextItems.Count == 0)
        {
            return null;
        }

        var limitedContext = LimitContext(contextItems, configuredOptions);
        if (limitedContext.Count == 0)
        {
            return null;
        }

        var stopwatch = Stopwatch.StartNew();
        var contextCharacters = limitedContext.Sum(item => item.Text.Length);

        AiPipelineLog.ChatAnswerGenerationStarted(
            logger,
            question.Length,
            limitedContext.Count,
            contextCharacters,
            configuredOptions.MaxOutputTokens);

        var prompt = BuildPrompt(question, limitedContext, configuredOptions);
        var messages = new[]
        {
            new DeepSeekMessage(
                "system",
                "You are Super Chat. Answer strictly in json. Use only the provided context. " +
                "Do not invent facts, sources, rooms, or times. If context is insufficient, say so plainly in assistant_text and keep items empty."),
            new DeepSeekMessage("user", prompt)
        };

        try
        {
            var response = await deepSeekJsonClient.CompleteJsonAsync<ChatAnswerJsonResponse>(
                messages,
                configuredOptions.MaxOutputTokens,
                cancellationToken);

            if (response is null)
            {
                stopwatch.Stop();
                AiPipelineLog.ChatAnswerGenerationCompleted(
                    logger,
                    limitedContext.Count,
                    0,
                    0,
                    stopwatch.ElapsedMilliseconds);
                return null;
            }

            var assistantText = response.AssistantText?.Trim();
            var knownKeys = limitedContext
                .Select(item => item.ReferenceKey)
                .ToHashSet(StringComparer.Ordinal);

            var items = (response.Items ?? [])
                .Where(item => !string.IsNullOrWhiteSpace(item.ReferenceKey))
                .Where(item => knownKeys.Contains(item.ReferenceKey))
                .Select(item => new GeneratedChatAnswerItem(
                    item.ReferenceKey,
                    NormalizeTitle(item.Title),
                    NormalizeSummary(item.Summary)))
                .Where(item => !string.IsNullOrWhiteSpace(item.Title) || !string.IsNullOrWhiteSpace(item.Summary))
                .DistinctBy(item => item.ReferenceKey, StringComparer.Ordinal)
                .Take(Math.Max(1, configuredOptions.MaxEvidenceItems))
                .ToList();

            if (string.IsNullOrWhiteSpace(assistantText) && items.Count == 0)
            {
                stopwatch.Stop();
                AiPipelineLog.ChatAnswerGenerationCompleted(
                    logger,
                    limitedContext.Count,
                    0,
                    0,
                    stopwatch.ElapsedMilliseconds);
                return null;
            }

            stopwatch.Stop();
            AiPipelineLog.ChatAnswerGenerationCompleted(
                logger,
                limitedContext.Count,
                items.Count,
                assistantText?.Length ?? 0,
                stopwatch.ElapsedMilliseconds);
            return new GeneratedChatAnswer(assistantText ?? string.Empty, items);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            AiPipelineLog.ChatAnswerGenerationFailed(
                logger,
                limitedContext.Count,
                stopwatch.ElapsedMilliseconds,
                exception);
            return null;
        }
    }

    private static IReadOnlyList<ChatAnswerContextItem> LimitContext(
        IReadOnlyList<ChatAnswerContextItem> contextItems,
        ChatAnsweringOptions options)
    {
        var results = new List<ChatAnswerContextItem>();
        var totalCharacters = 0;
        foreach (var contextItem in contextItems.Take(Math.Max(1, options.MaxContextChunks)))
        {
            var available = Math.Max(0, options.MaxContextCharacters - totalCharacters);
            if (available <= 0)
            {
                break;
            }

            var normalizedText = contextItem.Text.Trim();
            if (normalizedText.Length > available)
            {
                normalizedText = normalizedText[..available];
            }

            if (string.IsNullOrWhiteSpace(normalizedText))
            {
                continue;
            }

            results.Add(contextItem with { Text = normalizedText });
            totalCharacters += normalizedText.Length;
        }

        return results;
    }

    private static string BuildPrompt(
        string question,
        IReadOnlyList<ChatAnswerContextItem> contextItems,
        ChatAnsweringOptions options)
    {
        var contextJson = JsonSerializer.Serialize(
            contextItems.Select(item => new
            {
                reference_key = item.ReferenceKey,
                source_room = item.SourceRoom,
                timestamp = item.Timestamp?.ToUniversalTime().ToString("O"),
                text = item.Text
            }),
            JsonOptions);

        var builder = new StringBuilder();
        builder.AppendLine("Return a valid json object and nothing else.");
        builder.AppendLine();
        builder.AppendLine("JSON schema:");
        builder.AppendLine("{");
        builder.AppendLine("  \"assistant_text\": \"short answer in the user's language\",");
        builder.AppendLine("  \"items\": [");
        builder.AppendLine("    {");
        builder.AppendLine("      \"reference_key\": \"ctx_1\",");
        builder.AppendLine("      \"title\": \"short evidence title\",");
        builder.AppendLine("      \"summary\": \"why this evidence matters\"");
        builder.AppendLine("    }");
        builder.AppendLine("  ]");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("Rules:");
        builder.AppendLine("- Answer in the same language as the question.");
        builder.AppendLine("- Use only the provided context.");
        builder.AppendLine("- Do not invent new rooms, timestamps, or facts.");
        builder.AppendLine("- Use only reference_key values that exist in the context array.");
        builder.AppendLine("- assistant_text must be concise, at most 3 short sentences.");
        builder.AppendLine($"- Return at most {Math.Max(1, options.MaxEvidenceItems)} items.");
        builder.AppendLine("- If the context is insufficient, explain that in assistant_text and return an empty items array.");
        builder.AppendLine();
        builder.AppendLine("Question:");
        builder.AppendLine(question.Trim());
        builder.AppendLine();
        builder.AppendLine("Context:");
        builder.Append(contextJson);

        return builder.ToString();
    }

    private static string NormalizeTitle(string? title)
    {
        var normalized = title?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "Relevant context";
        }

        return normalized.Length <= 90
            ? normalized
            : $"{normalized[..87]}...";
    }

    private static string NormalizeSummary(string? summary)
    {
        var normalized = summary?.Trim() ?? string.Empty;
        return normalized.Length <= 280
            ? normalized
            : $"{normalized[..277]}...";
    }

    private sealed record ChatAnswerJsonResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("assistant_text")] string? AssistantText,
        [property: System.Text.Json.Serialization.JsonPropertyName("items")] IReadOnlyList<ChatAnswerJsonItem>? Items);

    private sealed record ChatAnswerJsonItem(
        [property: System.Text.Json.Serialization.JsonPropertyName("reference_key")] string ReferenceKey,
        [property: System.Text.Json.Serialization.JsonPropertyName("title")] string? Title,
        [property: System.Text.Json.Serialization.JsonPropertyName("summary")] string? Summary);
}
