namespace SuperChat.Contracts.Features.Operations;

public sealed record ProcessConversationAfterSettleCommand(
    Guid UserId,
    string Source,
    string MatrixRoomId);

public sealed record RebuildConversationChunksCommand(
    Guid UserId,
    string MatrixRoomId,
    DateTimeOffset RebuildFrom);

public sealed record IndexConversationChunksCommand(
    Guid UserId,
    string MatrixRoomId);

public sealed record ProjectConversationMeetingsCommand(
    Guid UserId,
    string MatrixRoomId);
