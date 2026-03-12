namespace SuperChat.Contracts.ViewModels;

public sealed record ChatAnswerViewModel(
    string Mode,
    string Question,
    IReadOnlyList<ChatResultItemViewModel> Items);
