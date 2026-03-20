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
        DateTimeOffset sentAt,
        CancellationToken cancellationToken);
}
