using System.Text.Json.Serialization;

namespace SuperChat.Infrastructure.Features.Intelligence.Retrieval;

internal sealed record LocalEmbedRequestDto(
    [property: JsonPropertyName("text")] string Text);

internal sealed record LocalEmbedResponseDto(
    [property: JsonPropertyName("dense_vector")] List<float> DenseVector,
    [property: JsonPropertyName("sparse_vector")] LocalSparseVectorResponseDto? SparseVector,
    [property: JsonPropertyName("provider")] string? Provider,
    [property: JsonPropertyName("model")] string? Model,
    [property: JsonPropertyName("embedding_version")] string? EmbeddingVersion);

internal sealed record LocalSparseVectorResponseDto(
    [property: JsonPropertyName("indices")] List<long> Indices,
    [property: JsonPropertyName("values")] List<float> Values);

internal sealed record YandexEmbedRequestDto(
    [property: JsonPropertyName("modelUri")] string ModelUri,
    [property: JsonPropertyName("text")] string Text);
