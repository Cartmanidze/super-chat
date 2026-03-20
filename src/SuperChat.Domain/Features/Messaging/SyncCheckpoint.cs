namespace SuperChat.Domain.Features.Messaging;

public sealed record SyncCheckpoint(
    Guid UserId,
    string? NextBatchToken,
    DateTimeOffset UpdatedAt);
