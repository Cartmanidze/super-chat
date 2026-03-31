using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts;
using SuperChat.Contracts.Features.Intelligence.Extraction;
using SuperChat.Contracts.Features.Intelligence.Retrieval;
using SuperChat.Infrastructure.Diagnostics;

namespace SuperChat.Infrastructure.Features.Chat;

public sealed class DeepSeekJsonClient(
    HttpClient httpClient,
    IOptions<DeepSeekOptions> options,
    ILogger<DeepSeekJsonClient> logger) : IDeepSeekJsonClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    public async Task<TResponse?> CompleteJsonAsync<TResponse>(
        IReadOnlyList<DeepSeekMessage> messages,
        int maxTokens,
        CancellationToken cancellationToken) where TResponse : class
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("DeepSeek API key is not configured.");
        }

        var configuredOptions = options.Value;
        double? normalizedTemperature = UsesReasoningModel(configuredOptions.Model) ? null : 0.1;
        var request = new DeepSeekChatCompletionRequest(
            configuredOptions.Model,
            messages,
            new DeepSeekResponseFormat("json_object"),
            Math.Max(1, maxTokens),
            normalizedTemperature);
        var promptCharacters = messages.Sum(item => item.Content.Length);
        var normalizedMaxTokens = Math.Max(1, maxTokens);
        var stopwatch = Stopwatch.StartNew();

        AiPipelineLog.DeepSeekRequestStarted(
            logger,
            configuredOptions.Model,
            messages.Count,
            promptCharacters,
            normalizedMaxTokens);

        try
        {
            using (var response = await httpClient.PostAsJsonAsync(
                       "/chat/completions",
                       request,
                       JsonOptions,
                       cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                var completion = await response.Content.ReadFromJsonAsync<DeepSeekChatCompletionResponse>(JsonOptions, cancellationToken);
                var content = completion?.Choices?
                    .FirstOrDefault()?
                    .Message?
                    .Content?
                    .Trim();

                stopwatch.Stop();
                AiPipelineLog.DeepSeekRequestCompleted(
                    logger,
                    configuredOptions.Model,
                    messages.Count,
                    completion?.Choices?.Count ?? 0,
                    content?.Length ?? 0,
                    stopwatch.ElapsedMilliseconds);

                if (string.IsNullOrWhiteSpace(content))
                {
                    logger.LogWarning("DeepSeek returned an empty JSON response.");
                    return null;
                }

                try
                {
                    var normalizedContent = NormalizeContentForLog(content);
                    AiPipelineLog.DeepSeekRawResponseReceived(
                        logger,
                        configuredOptions.Model,
                        content.Length,
                        normalizedContent);

                    var extractedJson = ExtractJsonObject(content);
                    AiPipelineLog.DeepSeekJsonPayloadExtracted(
                        logger,
                        configuredOptions.Model,
                        extractedJson.Length,
                        NormalizeContentForLog(extractedJson));

                    var parsed = JsonSerializer.Deserialize<TResponse>(extractedJson, JsonOptions);
                    AiPipelineLog.DeepSeekJsonParsed(
                        logger,
                        configuredOptions.Model,
                        typeof(TResponse).Name,
                        BuildParsedSummary(parsed));

                    if (parsed is DeepSeekStructuredResponse structuredResponse &&
                        (structuredResponse.Items?.Count ?? 0) == 0)
                    {
                        AiPipelineLog.DeepSeekStructuredResponseEmpty(
                            logger,
                            configuredOptions.Model,
                            content.Length,
                            content);
                    }

                    return parsed;
                }
                catch (JsonException exception)
                {
                    logger.LogWarning(exception, "DeepSeek returned invalid JSON content: {Content}", content);
                    throw;
                }
            }
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            var statusCode = exception is HttpRequestException httpRequestException
                ? httpRequestException.StatusCode?.ToString() ?? string.Empty
                : string.Empty;

            AiPipelineLog.DeepSeekRequestFailed(
                logger,
                configuredOptions.Model,
                messages.Count,
                promptCharacters,
                normalizedMaxTokens,
                stopwatch.ElapsedMilliseconds,
                statusCode,
                exception);

            throw;
        }
    }

    internal static string ExtractJsonObject(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstBrace = trimmed.IndexOf('{');
            var lastBrace = trimmed.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                return trimmed[firstBrace..(lastBrace + 1)];
            }
        }

        return trimmed;
    }

    internal static bool UsesReasoningModel(string model)
    {
        return !string.IsNullOrWhiteSpace(model) &&
               model.Contains("reasoner", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeContentForLog(string content)
    {
        const int maxLength = 4_000;

        var trimmed = content.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        return $"{trimmed[..maxLength]}... [truncated]";
    }

    private static string BuildParsedSummary<TResponse>(TResponse? parsed) where TResponse : class
    {
        if (parsed is null)
        {
            return "null";
        }

        if (parsed is DeepSeekStructuredResponse structuredResponse)
        {
            var itemCount = structuredResponse.Items?.Count ?? 0;
            var kinds = structuredResponse.Items is null
                ? "none"
                : string.Join(
                    ",",
                    structuredResponse.Items
                        .Select(item => item.Kind?.Trim())
                        .Where(kind => !string.IsNullOrWhiteSpace(kind))
                        .DefaultIfEmpty("none"));

            return $"items={itemCount}; kinds={kinds}";
        }

        return JsonSerializer.Serialize(parsed, JsonOptions);
    }
}
