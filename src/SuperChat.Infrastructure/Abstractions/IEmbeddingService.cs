namespace SuperChat.Infrastructure.Abstractions;

public interface IEmbeddingService
{
    Task<TextEmbedding> EmbedAsync(string text, CancellationToken cancellationToken);
}

public sealed record TextEmbedding(
    IReadOnlyList<float> DenseVector,
    SparseTextVector SparseVector,
    string Provider,
    string Model,
    string EmbeddingVersion);

public sealed record SparseTextVector(
    IReadOnlyList<int> Indices,
    IReadOnlyList<float> Values);
