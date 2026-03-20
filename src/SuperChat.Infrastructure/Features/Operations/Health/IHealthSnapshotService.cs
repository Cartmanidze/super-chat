namespace SuperChat.Infrastructure.Features.Operations.Health;

public interface IHealthSnapshotService
{
    Task<HealthSnapshot> GetAsync(CancellationToken cancellationToken);
}

public sealed record HealthSnapshot(
    int ActiveInviteCount,
    int KnownUserCount,
    int PendingMessageCount,
    int ExtractedItemCount,
    int ActiveSessionCount);
