using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Abstractions;

public interface IPipelineCommandScheduler
{
    bool RequiresTransactionalDispatch { get; }

    Task DispatchNormalizedMessageStoredAsync(
        SuperChatDbContext dbContext,
        Guid userId,
        string source,
        string matrixRoomId,
        Guid normalizedMessageId,
        string matrixEventId,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken);
}
