namespace SuperChat.Domain.Features.Messaging;

public sealed record NormalizedMessage(
    Guid Id,
    Guid UserId,
    string Source,
    string MatrixRoomId,
    string MatrixEventId,
    string SenderName,
    string Text,
    DateTimeOffset SentAt,
    DateTimeOffset IngestedAt,
    bool Processed);
