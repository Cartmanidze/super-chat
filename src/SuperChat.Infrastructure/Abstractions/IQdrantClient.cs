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
    IReadOnlyDictionary<string, object?> Payload);

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
    double Score,
    IReadOnlyDictionary<string, object?> Payload);
