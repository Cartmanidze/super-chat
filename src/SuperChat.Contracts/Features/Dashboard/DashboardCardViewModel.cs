namespace SuperChat.Contracts.ViewModels;

public sealed record DashboardCardViewModel(
    string Title,
    string Summary,
    string Kind,
    DateTimeOffset? DueAt,
    string SourceRoom);
