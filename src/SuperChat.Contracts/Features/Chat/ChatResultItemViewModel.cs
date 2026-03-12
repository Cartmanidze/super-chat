namespace SuperChat.Contracts.ViewModels;

public sealed record ChatResultItemViewModel(
    string Title,
    string Summary,
    string SourceRoom,
    DateTimeOffset? Timestamp);
