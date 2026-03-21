namespace SuperChat.Contracts.Features.Operations;

public sealed record ProcessConversationAfterSettleCommand(
    Guid UserId,
    string Source,
    string MatrixRoomId);

public sealed record ResolveConversationItemsCommand(
    Guid UserId,
    string MatrixRoomId);

public sealed record ResolveDueMeetingsCommand(
    Guid UserId,
    string MatrixRoomId,
    DateTimeOffset ResolveAfter);

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
