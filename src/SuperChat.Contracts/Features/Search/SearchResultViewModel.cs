namespace SuperChat.Contracts.ViewModels;

public sealed record SearchResultViewModel(
    string Title,
    string Summary,
    string Kind,
    string SourceRoom,
    DateTimeOffset ObservedAt);
