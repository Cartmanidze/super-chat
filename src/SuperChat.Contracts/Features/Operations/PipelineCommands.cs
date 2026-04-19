namespace SuperChat.Contracts.Features.Operations;

public sealed record ProcessConversationAfterSettleCommand(
    Guid UserId,
    string Source,
    string ExternalChatId,
    Guid? TriggerMessageId = null,
    string? TriggerExternalMessageId = null);

public sealed record ResolveConversationItemsCommand(
    Guid UserId,
    string ExternalChatId,
    Guid? TriggerMessageId = null,
    string? TriggerExternalMessageId = null);

public sealed record ResolveDueMeetingsCommand(
    Guid UserId,
    string ExternalChatId,
    DateTimeOffset ResolveAfter,
    Guid? TriggerMessageId = null,
    string? TriggerExternalMessageId = null);

public sealed record RebuildConversationChunksCommand(
    Guid UserId,
    string ExternalChatId,
    DateTimeOffset RebuildFrom,
    Guid? TriggerMessageId = null,
    string? TriggerExternalMessageId = null);

public sealed record IndexConversationChunksCommand(
    Guid UserId,
    string ExternalChatId,
    Guid? TriggerMessageId = null,
    string? TriggerExternalMessageId = null);

public sealed record ProjectConversationMeetingsCommand(
    Guid UserId,
    string ExternalChatId,
    Guid? TriggerMessageId = null,
    string? TriggerExternalMessageId = null);
