using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.State;

namespace SuperChat.Infrastructure.Services;

public sealed class MessageNormalizationService(SuperChatStore store) : IMessageNormalizationService
{
    public IReadOnlyList<NormalizedMessage> GetPendingMessages()
    {
        return store.GetPendingMessages();
    }

    public void MarkProcessed(IEnumerable<Guid> messageIds)
    {
        store.MarkMessagesProcessed(messageIds);
    }

    public bool TryStore(Guid userId, string roomId, string eventId, string senderName, string text, DateTimeOffset sentAt)
    {
        var now = DateTimeOffset.UtcNow;
        var message = new NormalizedMessage(
            Guid.NewGuid(),
            userId,
            "telegram",
            roomId,
            eventId,
            senderName,
            text,
            sentAt,
            now,
            false);

        return store.TryAddMessage(message);
    }
}
