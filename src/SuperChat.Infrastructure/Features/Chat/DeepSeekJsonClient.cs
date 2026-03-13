using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

public sealed class DeepSeekJsonClient(
    HttpClient httpClient,
    IOptions<DeepSeekOptions> options,
    ILogger<DeepSeekJsonClient> logger) : IDeepSeekJsonClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
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
        var request = new DeepSeekChatCompletionRequest(
            configuredOptions.Model,
            messages,
            new DeepSeekResponseFormat("json_object"),
            Math.Max(1, maxTokens),
            0.1);

        using var response = await httpClient.PostAsJsonAsync(
            "/chat/completions",
            request,
            JsonOptions,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var completion = await response.Content.ReadFromJsonAsync<DeepSeekChatCompletionResponse>(JsonOptions, cancellationToken);
        var content = completion?.Choices?
            .FirstOrDefault()?
            .Message?
            .Content?
            .Trim();

        if (string.IsNullOrWhiteSpace(content))
        {
            logger.LogWarning("DeepSeek returned an empty JSON response.");
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TResponse>(ExtractJsonObject(content), JsonOptions);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "DeepSeek returned invalid JSON content: {Content}", content);
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
}
