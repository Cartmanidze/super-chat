using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

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
        return configuredProvider switch
        {
            "localservice" => await EmbedViaLocalServiceAsync(text, cancellationToken),
            "yandexcloud" => await EmbedViaYandexCloudAsync(text, purpose, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported embedding backend: {options.Value.Backend}")
        };
    }

    private async Task<TextEmbedding> EmbedViaLocalServiceAsync(string text, CancellationToken cancellationToken)
    {
        using (var response = await httpClient.PostAsJsonAsync(
                   "/embed",
                   new LocalEmbedRequest(text),
                   cancellationToken))
        {
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<LocalEmbedResponse>(cancellationToken)
                ?? throw new InvalidOperationException("Embedding service returned an empty payload.");

            if (payload.DenseVector is null || payload.DenseVector.Count == 0)
            {
                throw new InvalidOperationException("Embedding service returned an empty dense vector.");
            }

            ValidateDenseVectorSize(payload.DenseVector.Count);

            var sparseVector = payload.SparseVector
                ?? throw new InvalidOperationException("Embedding service returned no sparse vector.");

            if (sparseVector.Indices.Count != sparseVector.Values.Count)
            {
                throw new InvalidOperationException("Embedding service returned mismatched sparse vector arrays.");
            }

            return new TextEmbedding(
                payload.DenseVector,
                new SparseTextVector(sparseVector.Indices, sparseVector.Values),
                payload.Provider ?? "local_service",
                payload.Model ?? string.Empty,
                payload.EmbeddingVersion ?? string.Empty);
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

        using (var request = new HttpRequestMessage(HttpMethod.Post, "/foundationModels/v1/textEmbedding")
        {
            Content = JsonContent.Create(new YandexEmbedRequest(modelUri, text))
        })
        {
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

                        var denseVector = ParseDenseVector(embeddingElement);
                        if (denseVector.Count == 0)
                        {
                            throw new InvalidOperationException("Yandex Cloud embedding API returned an empty dense vector.");
                        }

                        ValidateDenseVectorSize(denseVector.Count);

                        var sparseVector = LexicalSparseVectorBuilder.Build(text);
                        var modelVersion = root.TryGetProperty("modelVersion", out var modelVersionElement)
                            ? modelVersionElement.GetString()
                            : null;

                        return new TextEmbedding(
                            denseVector,
                            sparseVector,
                            "yandex_cloud",
                            modelUri,
                            BuildYandexEmbeddingVersion(modelUri, modelVersion));
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

    private static List<float> ParseDenseVector(JsonElement embeddingElement)
    {
        var denseVector = new List<float>(embeddingElement.GetArrayLength());

        foreach (var item in embeddingElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Number)
            {
                denseVector.Add(item.GetSingle());
                continue;
            }

            if (item.ValueKind == JsonValueKind.String &&
                float.TryParse(item.GetString(), out var parsedValue))
            {
                denseVector.Add(parsedValue);
                continue;
            }

            throw new InvalidOperationException("Yandex Cloud embedding API returned a non-numeric vector item.");
        }

        return denseVector;
    }

    private static string BuildYandexEmbeddingVersion(string modelUri, string? modelVersion)
    {
        return string.IsNullOrWhiteSpace(modelVersion)
            ? modelUri
            : $"{modelUri}:{modelVersion.Trim()}";
    }

    private sealed record LocalEmbedRequest([property: JsonPropertyName("text")] string Text);

    private sealed record LocalEmbedResponse(
        [property: JsonPropertyName("dense_vector")] List<float> DenseVector,
        [property: JsonPropertyName("sparse_vector")] LocalSparseVectorResponse? SparseVector,
        [property: JsonPropertyName("provider")] string? Provider,
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("embedding_version")] string? EmbeddingVersion);

    private sealed record LocalSparseVectorResponse(
        [property: JsonPropertyName("indices")] List<long> Indices,
        [property: JsonPropertyName("values")] List<float> Values);

    private sealed record YandexEmbedRequest(
        [property: JsonPropertyName("modelUri")] string ModelUri,
        [property: JsonPropertyName("text")] string Text);
}
