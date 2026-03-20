namespace SuperChat.Contracts.Features.Chat;

public sealed record ChatAnswerViewModel(
    string Mode,
    string Question,
    IReadOnlyList<ChatResultItemViewModel> Items,
    string? AssistantText = null);
