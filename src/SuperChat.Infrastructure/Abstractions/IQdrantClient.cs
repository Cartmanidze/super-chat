namespace SuperChat.Infrastructure.Abstractions;

public interface IQdrantClient
{
    Task EnsureMemoryCollectionAsync(CancellationToken cancellationToken);

    Task UpsertMemoryPointsAsync(IReadOnlyList<QdrantMemoryPoint> points, CancellationToken cancellationToken);

    Task<IReadOnlyList<QdrantQueryPoint>> QueryMemoryPointsAsync(QdrantHybridQuery request, CancellationToken cancellationToken);
}

public sealed record QdrantMemoryPoint(
    string PointId,
    IReadOnlyList<float> DenseVector,
    SparseTextVector SparseVector,
    QdrantChunkPayload Payload);

public sealed record QdrantHybridQuery(
    IReadOnlyList<float> DenseVector,
    SparseTextVector SparseVector,
    string UserId,
    string? ChatId,
    string? PeerId,
    string? Kind,
    int PrefetchLimit,
    int Limit);

public sealed record QdrantQueryPoint(
    string PointId,
    string? ChunkId,
    double Score);

public sealed record QdrantChunkPayload
{
    public string UserId { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public string Provider { get; init; } = string.Empty;

    public string Transport { get; init; } = string.Empty;

    public string ChatId { get; init; } = string.Empty;

    public string? PeerId { get; init; }

    public string? ThreadId { get; init; }

    public string Kind { get; init; } = string.Empty;

    public long TsFrom { get; init; }

    public long TsTo { get; init; }

    public string ChunkId { get; init; } = string.Empty;

    public string EmbeddingVersion { get; init; } = string.Empty;

    public int ChunkVersion { get; init; }

    public int MessageCount { get; init; }

    public string ContentHash { get; init; } = string.Empty;
}
