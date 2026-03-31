namespace SuperChat.Contracts.Features.Intelligence.Retrieval;

public interface IChunkBuilderService
{
    Task<ChunkBuildRunResult> BuildPendingChunksAsync(CancellationToken cancellationToken);

    Task<ChunkBuildRunResult> BuildConversationChunksAsync(
        Guid userId,
        string matrixRoomId,
        DateTimeOffset rebuildFrom,
        CancellationToken cancellationToken);
}

public sealed record ChunkBuildRunResult(
    int UsersProcessed,
    int RoomsRebuilt,
    int ChunksWritten,
    int MessagesConsidered)
{
    public static ChunkBuildRunResult Empty { get; } = new(0, 0, 0, 0);

    public ChunkBuildRunResult Merge(ChunkBuildRunResult other)
    {
        return new ChunkBuildRunResult(
            UsersProcessed + other.UsersProcessed,
            RoomsRebuilt + other.RoomsRebuilt,
            ChunksWritten + other.ChunksWritten,
            MessagesConsidered + other.MessagesConsidered);
    }
}
