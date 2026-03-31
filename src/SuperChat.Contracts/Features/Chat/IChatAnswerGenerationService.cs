namespace SuperChat.Contracts.Features.Chat;

public interface IChatAnswerGenerationService
{
    Task<GeneratedChatAnswer?> TryGenerateAsync(
        string question,
        IReadOnlyList<ChatAnswerContextItem> contextItems,
        CancellationToken cancellationToken);
}

public sealed record ChatAnswerContextItem(
    string ReferenceKey,
    string SourceRoom,
    DateTimeOffset? Timestamp,
    string Text);

public sealed record GeneratedChatAnswer(
    string AssistantText,
    IReadOnlyList<GeneratedChatAnswerItem> Items);

public sealed record GeneratedChatAnswerItem(
    string ReferenceKey,
    string Title,
    string Summary);
