using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Abstractions;

public interface IMessageNormalizationService
{
    bool TryStore(Guid userId, string roomId, string eventId, string senderName, string text, DateTimeOffset sentAt);

    IReadOnlyList<NormalizedMessage> GetPendingMessages();

    void MarkProcessed(IEnumerable<Guid> messageIds);
}
