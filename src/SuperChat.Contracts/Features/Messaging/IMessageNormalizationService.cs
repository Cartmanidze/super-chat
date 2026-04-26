using SuperChat.Domain.Features.Messaging;

namespace SuperChat.Contracts.Features.Messaging;

public interface IMessageNormalizationService
{
    Task<bool> TryStoreAsync(
        Guid userId,
        string source,
        string externalChatId,
        string externalMessageId,
        string senderName,
        string text,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NormalizedMessage>> GetPendingMessagesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<NormalizedMessage>> GetPendingMessagesForConversationAsync(
        Guid userId,
        string source,
        string externalChatId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NormalizedMessage>> GetRecentMessagesAsync(Guid userId, int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<NormalizedMessage>> SearchRecentMessagesAsync(Guid userId, string query, int limit, CancellationToken cancellationToken);

    Task MarkProcessedAsync(IEnumerable<Guid> messageIds, CancellationToken cancellationToken);
}
