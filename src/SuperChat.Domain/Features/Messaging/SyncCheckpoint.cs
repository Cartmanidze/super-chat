namespace SuperChat.Domain.Model;

public sealed record SyncCheckpoint(
    Guid UserId,
    string? NextBatchToken,
    DateTimeOffset UpdatedAt);
