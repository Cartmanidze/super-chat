namespace SuperChat.Contracts.ViewModels;

public sealed record DashboardCardViewModel(
    string Title,
    string Summary,
    string Kind,
    DateTimeOffset ObservedAt,
    DateTimeOffset? DueAt,
    string SourceRoom,
    double Confidence = 0d);
