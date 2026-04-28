namespace SuperChat.Infrastructure.Features.Chat;

internal sealed record RetrievedChatContext(
    string Title,
    string Summary,
    string ExternalChatId,
    DateTimeOffset ObservedAt,
    string Text);
