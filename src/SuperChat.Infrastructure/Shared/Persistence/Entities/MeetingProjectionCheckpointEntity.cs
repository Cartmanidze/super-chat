namespace SuperChat.Infrastructure.Shared.Persistence;

internal sealed class MeetingProjectionCheckpointEntity
{
    public Guid UserId { get; set; }
    public DateTimeOffset? LastObservedChunkUpdatedAt { get; set; }
    public Guid? LastObservedChunkId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
