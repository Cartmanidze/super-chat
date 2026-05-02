using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Abstractions;

public interface IPipelineCommandScheduler
{
    bool RequiresTransactionalDispatch { get; }

    Task DispatchChatMessageStoredAsync(
        SuperChatDbContext dbContext,
        ChatMessageStoredEvent payload,
        CancellationToken cancellationToken);
}
