using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Abstractions;

public interface IPipelineCommandScheduler
{
    bool RequiresTransactionalDispatch { get; }

    Task DispatchNormalizedMessageStoredAsync(
        SuperChatDbContext dbContext,
        Guid userId,
        string source,
        string externalChatId,
        Guid normalizedMessageId,
        string externalMessageId,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken);
}
