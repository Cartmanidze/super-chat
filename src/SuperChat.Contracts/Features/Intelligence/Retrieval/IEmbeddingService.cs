namespace SuperChat.Contracts.Features.Intelligence.Retrieval;

public enum EmbeddingPurpose
{
    Document = 0,
    Query = 1
}

public interface IEmbeddingService
{
    Task<TextEmbedding> EmbedAsync(string text, EmbeddingPurpose purpose, CancellationToken cancellationToken);
}

public sealed record TextEmbedding(
    IReadOnlyList<float> DenseVector,
    SparseTextVector SparseVector,
    string Provider,
    string Model,
    string EmbeddingVersion);

public sealed record SparseTextVector(
    IReadOnlyList<long> Indices,
    IReadOnlyList<float> Values);
