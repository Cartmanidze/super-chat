using SuperChat.Domain.Features.Messaging;

namespace SuperChat.Infrastructure.Features.Messaging;

public interface IMessageNormalizationService
{
    Task<bool> TryStoreAsync(
        Guid userId,
        string roomId,
        string eventId,
        string senderName,
        string text,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NormalizedMessage>> GetPendingMessagesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<NormalizedMessage>> GetPendingMessagesForConversationAsync(
        Guid userId,
        string source,
        string matrixRoomId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NormalizedMessage>> GetRecentMessagesAsync(Guid userId, int take, CancellationToken cancellationToken);

    Task MarkProcessedAsync(IEnumerable<Guid> messageIds, CancellationToken cancellationToken);
}
