using SuperChat.Domain.Features.Messaging;

namespace SuperChat.Contracts.Features.Messaging;

public interface IChatMessageStore
{
    Task<bool> TryStoreAsync(
        Guid userId,
        string source,
        string externalChatId,
        string externalMessageId,
        string senderName,
        string text,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken,
        string? chatTitle = null);

    Task<IReadOnlyList<ChatMessage>> GetPendingMessagesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatMessage>> GetPendingMessagesForConversationAsync(
        Guid userId,
        string source,
        string externalChatId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatMessage>> GetRecentMessagesAsync(Guid userId, int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatMessage>> SearchRecentMessagesAsync(Guid userId, string query, int limit, CancellationToken cancellationToken);

    Task MarkProcessedAsync(IEnumerable<Guid> messageIds, CancellationToken cancellationToken);
}
