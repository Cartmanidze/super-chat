using SuperChat.Contracts.Features.WorkItems;

namespace SuperChat.Web.Pages;

internal static class WorkItemCardMappings
{
    public static TodayModel.TodayCard ToWorkItemCard(this WorkItemCardViewModel card, string hint)
    {
        var timestamp = card.DueAt ?? card.ObservedAt;
        var searchQuery = BuildSearchQuery(card.Title, card.Summary, card.SourceRoom);

        return new TodayModel.TodayCard(
            card.Id,
            card.Title,
            card.Summary,
            card.SourceRoom,
            timestamp,
            hint,
            searchQuery,
            card.Confidence);
    }

    private static string BuildSearchQuery(string title, string summary, string sourceRoom)
    {
        foreach (var candidate in new[] { title, summary, sourceRoom })
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                var value = candidate.Trim();
                return value.Length <= 80 ? value : value[..80];
            }
        }

        return string.Empty;
    }
}
