namespace SuperChat.Contracts.Features.Messaging;

public sealed record IncomingChatMessage(
    Guid UserId,
    ChatSourceKind Source,
    string ExternalChatId,
    string ExternalMessageId,
    string SenderName,
    string Text,
    DateTimeOffset SentAt);
