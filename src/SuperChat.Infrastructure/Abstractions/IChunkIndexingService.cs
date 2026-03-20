namespace SuperChat.Infrastructure.Abstractions;

public interface IChunkIndexingService
{
    Task<ChunkIndexingRunResult> IndexPendingChunksAsync(CancellationToken cancellationToken);

    Task<ChunkIndexingRunResult> IndexConversationChunksAsync(
        Guid userId,
        string matrixRoomId,
        CancellationToken cancellationToken);
}

public sealed record ChunkIndexingRunResult(
    int ChunksSelected,
    int ChunksIndexed)
{
    public static ChunkIndexingRunResult Empty { get; } = new(0, 0);
}
