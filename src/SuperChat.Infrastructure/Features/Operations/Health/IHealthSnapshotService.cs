namespace SuperChat.Infrastructure.Abstractions;

public interface IHealthSnapshotService
{
    Task<HealthSnapshot> GetAsync(CancellationToken cancellationToken);
}

public sealed record HealthSnapshot(
    int AllowedEmailCount,
    int KnownUserCount,
    int PendingMessageCount,
    int ExtractedItemCount,
    int ActiveSessionCount);
