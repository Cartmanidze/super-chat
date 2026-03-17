using System.Text.Json;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

internal static class EmbeddingResponseMappings
{
    public static TextEmbedding ToTextEmbedding(this LocalEmbedResponseDto payload)
    {
        if (payload.DenseVector is null || payload.DenseVector.Count == 0)
        {
            throw new InvalidOperationException("Embedding service returned an empty dense vector.");
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
            payload.Provider ?? "local_service",
            payload.Model ?? string.Empty,
            payload.EmbeddingVersion ?? string.Empty);
    }

    public static TextEmbedding ToYandexTextEmbedding(
        this JsonElement root,
        string sourceText,
        string modelUri,
        IReadOnlyList<float> denseVector)
    {
        var sparseVector = LexicalSparseVectorBuilder.Build(sourceText);
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

    public static List<float> ToDenseVector(this JsonElement embeddingElement)
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
}
