namespace SuperChat.Infrastructure.Abstractions;

public interface IChunkBuilderService
{
    Task<ChunkBuildRunResult> BuildPendingChunksAsync(CancellationToken cancellationToken);
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
