namespace SuperChat.Infrastructure.Features.Chat;

internal sealed record RetrievedChatContext(
    string Title,
    string Summary,
    string SourceRoom,
    DateTimeOffset ObservedAt,
    string Text);
