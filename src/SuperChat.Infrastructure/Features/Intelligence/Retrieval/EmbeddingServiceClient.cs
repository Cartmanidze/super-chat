using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Intelligence.Retrieval;
using SuperChat.Infrastructure.Diagnostics;

namespace SuperChat.Infrastructure.Features.Intelligence.Retrieval;

public sealed class EmbeddingServiceClient(
    HttpClient httpClient,
    IOptions<EmbeddingOptions> options,
    ILogger<EmbeddingServiceClient> logger) : IEmbeddingService
{
    public async Task<TextEmbedding> EmbedAsync(string text, EmbeddingPurpose purpose, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            throw new InvalidOperationException("Embedding service is disabled.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Embedding text must not be empty.", nameof(text));
        }

        var configuredProvider = NormalizeProvider(options.Value.Backend);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var purposeName = purpose.ToString();

        AiPipelineLog.EmbeddingRequestStarted(logger, configuredProvider, purposeName, text.Length);

        try
        {
            var embedding = configuredProvider switch
            {
                "localservice" => await EmbedViaLocalServiceAsync(text, cancellationToken),
                "yandexcloud" => await EmbedViaYandexCloudAsync(text, purpose, cancellationToken),
                _ => throw new InvalidOperationException($"Unsupported embedding backend: {options.Value.Backend}")
            };

            stopwatch.Stop();
            AiPipelineLog.EmbeddingRequestCompleted(
                logger,
                configuredProvider,
                purposeName,
                text.Length,
                embedding.DenseVector.Count,
                embedding.SparseVector.Values.Count,
                string.IsNullOrWhiteSpace(embedding.Model) ? "unknown" : embedding.Model,
                stopwatch.ElapsedMilliseconds);

            return embedding;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            var statusCode = exception is HttpRequestException httpRequestException
                ? httpRequestException.StatusCode?.ToString() ?? string.Empty
                : string.Empty;

            AiPipelineLog.EmbeddingRequestFailed(
                logger,
                configuredProvider,
                purposeName,
                text.Length,
                stopwatch.ElapsedMilliseconds,
                statusCode,
                exception);

            throw;
        }
    }

    private async Task<TextEmbedding> EmbedViaLocalServiceAsync(string text, CancellationToken cancellationToken)
    {
        using (var response = await httpClient.PostAsJsonAsync(
                   "/embed",
                   new LocalEmbedRequestDto(text),
                   cancellationToken))
        {
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<LocalEmbedResponseDto>(cancellationToken)
                ?? throw new InvalidOperationException("Embedding service returned an empty payload.");

            ValidateDenseVectorSize(payload.DenseVector?.Count ?? 0);
            return payload.ToTextEmbedding();
        }
    }

    private async Task<TextEmbedding> EmbedViaYandexCloudAsync(
        string text,
        EmbeddingPurpose purpose,
        CancellationToken cancellationToken)
    {
        var configuredOptions = options.Value;
        if (string.IsNullOrWhiteSpace(configuredOptions.YandexApiKey))
        {
            throw new InvalidOperationException("Yandex Cloud embedding API key is not configured.");
        }

        var modelUri = ResolveYandexModelUri(configuredOptions, purpose);
        if (string.IsNullOrWhiteSpace(modelUri))
        {
            throw new InvalidOperationException("Yandex Cloud embedding model URI is not configured.");
        }

        using (var request = new HttpRequestMessage(HttpMethod.Post, "/foundationModels/v1/textEmbedding"))
        {
            request.Content = JsonContent.Create(new YandexEmbedRequestDto(modelUri, text));
            request.Headers.Authorization = new AuthenticationHeaderValue("Api-Key", configuredOptions.YandexApiKey);

            using (var response = await httpClient.SendAsync(request, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                await using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
                {
                    using (var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken))
                    {
                        var root = document.RootElement;

                        if (!root.TryGetProperty("embedding", out var embeddingElement) || embeddingElement.ValueKind != JsonValueKind.Array)
                        {
                            throw new InvalidOperationException("Yandex Cloud embedding API returned no embedding vector.");
                        }

                        var denseVector = embeddingElement.ToDenseVector();
                        if (denseVector.Count == 0)
                        {
                            throw new InvalidOperationException("Yandex Cloud embedding API returned an empty dense vector.");
                        }

                        ValidateDenseVectorSize(denseVector.Count);
                        return root.ToYandexTextEmbedding(text, modelUri, denseVector);
                    }
                }
            }
        }
    }

    private void ValidateDenseVectorSize(int actualSize)
    {
        if (options.Value.DenseVectorSize > 0 && actualSize != options.Value.DenseVectorSize)
        {
            logger.LogWarning(
                "Embedding service returned dense vector size {ActualSize}, expected {ExpectedSize}.",
                actualSize,
                options.Value.DenseVectorSize);
        }
    }

    private static string NormalizeProvider(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return string.Empty;
        }

        return provider.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();
    }

    private static string ResolveYandexModelUri(EmbeddingOptions configuredOptions, EmbeddingPurpose purpose)
    {
        var explicitUri = purpose == EmbeddingPurpose.Query
            ? configuredOptions.YandexQueryModelUri
            : configuredOptions.YandexDocModelUri;

        if (!string.IsNullOrWhiteSpace(explicitUri))
        {
            return explicitUri.Trim();
        }

        if (string.IsNullOrWhiteSpace(configuredOptions.YandexFolderId))
        {
            return string.Empty;
        }

        var modelName = purpose == EmbeddingPurpose.Query
            ? configuredOptions.YandexQueryModelName
            : configuredOptions.YandexDocModelName;

        return $"emb://{configuredOptions.YandexFolderId.Trim()}/{modelName.Trim()}/latest";
    }

}
