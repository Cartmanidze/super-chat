namespace SuperChat.Infrastructure.Shared.Persistence;

internal sealed class SyncCheckpointEntity
{
    public Guid UserId { get; set; }
    public string? NextBatchToken { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
