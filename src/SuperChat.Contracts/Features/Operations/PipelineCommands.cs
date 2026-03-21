namespace SuperChat.Contracts.Features.Operations;

public sealed record ProcessConversationAfterSettleCommand(
    Guid UserId,
    string Source,
    string MatrixRoomId,
    Guid? TriggerMessageId = null,
    string? TriggerMatrixEventId = null);

public sealed record ResolveConversationItemsCommand(
    Guid UserId,
    string MatrixRoomId,
    Guid? TriggerMessageId = null,
    string? TriggerMatrixEventId = null);

public sealed record ResolveDueMeetingsCommand(
    Guid UserId,
    string MatrixRoomId,
    DateTimeOffset ResolveAfter,
    Guid? TriggerMessageId = null,
    string? TriggerMatrixEventId = null);

public sealed record RebuildConversationChunksCommand(
    Guid UserId,
    string MatrixRoomId,
    DateTimeOffset RebuildFrom,
    Guid? TriggerMessageId = null,
    string? TriggerMatrixEventId = null);

public sealed record IndexConversationChunksCommand(
    Guid UserId,
    string MatrixRoomId,
    Guid? TriggerMessageId = null,
    string? TriggerMatrixEventId = null);

public sealed record ProjectConversationMeetingsCommand(
    Guid UserId,
    string MatrixRoomId,
    Guid? TriggerMessageId = null,
    string? TriggerMatrixEventId = null);
