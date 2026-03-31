namespace SuperChat.Infrastructure.Shared.Persistence;

internal sealed class ChunkBuildCheckpointEntity
{
    public Guid UserId { get; set; }
    public DateTimeOffset? LastObservedIngestedAt { get; set; }
    public Guid? LastObservedMessageId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
