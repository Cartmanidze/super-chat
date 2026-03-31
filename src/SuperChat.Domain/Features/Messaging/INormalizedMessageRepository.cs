namespace SuperChat.Domain.Features.Messaging;

public interface INormalizedMessageRepository
{
    Task<bool> ExistsAsync(Guid userId, string matrixRoomId, string matrixEventId, CancellationToken cancellationToken);
    Task AddAsync(NormalizedMessage message, CancellationToken cancellationToken);
    Task<IReadOnlyList<NormalizedMessage>> GetPendingAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<NormalizedMessage>> GetPendingForConversationAsync(Guid userId, string source, string matrixRoomId, CancellationToken cancellationToken);
    Task<IReadOnlyList<NormalizedMessage>> GetRecentAsync(Guid userId, int take, CancellationToken cancellationToken);
    Task MarkProcessedAsync(IReadOnlyList<Guid> messageIds, CancellationToken cancellationToken);
}
