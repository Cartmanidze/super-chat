using SuperChat.Domain.Shared;

namespace SuperChat.Domain.Features.Messaging;

public sealed record NormalizedMessage(
    Guid Id,
    Guid UserId,
    string Source,
    string ExternalChatId,
    string ExternalMessageId,
    string SenderName,
    string Text,
    DateTimeOffset SentAt,
    DateTimeOffset ReceivedAt,
    bool Processed)
{
    private readonly bool _validated = Validate(Id, UserId, Source, ExternalChatId, ExternalMessageId, SenderName);

    private static bool Validate(Guid id, Guid userId, string source, string externalChatId, string externalMessageId, string senderName)
    {
        DomainGuard.NotEmpty(id);
        DomainGuard.NotEmpty(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalChatId);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalMessageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(senderName);
        return true;
    }
}
