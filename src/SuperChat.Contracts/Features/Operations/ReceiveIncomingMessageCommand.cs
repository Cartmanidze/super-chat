using SuperChat.Contracts.Features.Messaging;

namespace SuperChat.Contracts.Features.Operations;

public sealed record ReceiveIncomingMessageCommand(
    Guid UserId,
    ChatSourceKind Source,
    string ExternalChatId,
    string ExternalMessageId,
    string SenderName,
    string Text,
    DateTimeOffset SentAt);
