namespace SuperChat.Contracts.ViewModels;

public sealed record ConnectionStatusViewModel(
    string State,
    string Summary,
    string? MatrixUserId,
    Uri? WebLoginUrl,
    DateTimeOffset? LastSyncedAt);
