using SuperChat.Domain.Shared;

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
    bool Processed)
{
    private readonly bool _validated = Validate(Id, UserId, Source, MatrixRoomId, MatrixEventId, SenderName);

    private static bool Validate(Guid id, Guid userId, string source, string matrixRoomId, string matrixEventId, string senderName)
    {
        DomainGuard.NotEmpty(id);
        DomainGuard.NotEmpty(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(matrixRoomId);
        ArgumentException.ThrowIfNullOrWhiteSpace(matrixEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(senderName);
        return true;
    }
}
