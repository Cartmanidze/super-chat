namespace SuperChat.Web.Pages;

public sealed record ResolvedHistoryCard(
    string Title,
    string Summary,
    string ChatLabel,
    DateTimeOffset ResolvedAt,
    string ResolutionNote,
    string SearchQuery);
