using System.Net.Http.Json;
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
    public async Task<TextEmbedding> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            throw new InvalidOperationException("Embedding service is disabled.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Embedding text must not be empty.", nameof(text));
        }

        using var response = await httpClient.PostAsJsonAsync(
            "/embed",
            new EmbedRequest(text),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<EmbedResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Embedding service returned an empty payload.");

        if (payload.DenseVector is null || payload.DenseVector.Count == 0)
        {
            throw new InvalidOperationException("Embedding service returned an empty dense vector.");
        }

        if (options.Value.DenseVectorSize > 0 && payload.DenseVector.Count != options.Value.DenseVectorSize)
        {
            logger.LogWarning(
                "Embedding service returned dense vector size {ActualSize}, expected {ExpectedSize}.",
                payload.DenseVector.Count,
                options.Value.DenseVectorSize);
        }

        var sparseVector = payload.SparseVector
            ?? throw new InvalidOperationException("Embedding service returned no sparse vector.");

        if (sparseVector.Indices.Count != sparseVector.Values.Count)
        {
            throw new InvalidOperationException("Embedding service returned mismatched sparse vector arrays.");
        }

        return new TextEmbedding(
            payload.DenseVector,
            new SparseTextVector(sparseVector.Indices, sparseVector.Values),
            payload.Provider ?? "unknown",
            payload.Model ?? string.Empty,
            payload.EmbeddingVersion ?? string.Empty);
    }

    private sealed record EmbedRequest([property: JsonPropertyName("text")] string Text);

    private sealed record EmbedResponse(
        [property: JsonPropertyName("dense_vector")] List<float> DenseVector,
        [property: JsonPropertyName("sparse_vector")] SparseVectorResponse? SparseVector,
        [property: JsonPropertyName("provider")] string? Provider,
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("embedding_version")] string? EmbeddingVersion);

    private sealed record SparseVectorResponse(
        [property: JsonPropertyName("indices")] List<long> Indices,
        [property: JsonPropertyName("values")] List<float> Values);
}
