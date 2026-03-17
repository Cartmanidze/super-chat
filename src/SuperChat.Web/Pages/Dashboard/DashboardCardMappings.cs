using SuperChat.Contracts.ViewModels;
using SuperChat.Domain.Model;

namespace SuperChat.Web.Pages.Dashboard;

internal static class DashboardCardMappings
{
    public static TodayModel.DashboardCard ToDashboardCard(this DashboardCardViewModel card, string hint)
    {
        var timestamp = card.DueAt ?? card.ObservedAt;
        var searchQuery = BuildSearchQuery(card.Title, card.Summary, card.SourceRoom);

        return new TodayModel.DashboardCard(
            card.Title,
            card.Summary,
            card.SourceRoom,
            timestamp,
            hint,
            searchQuery,
            card.Confidence);
    }

    public static TodayModel.DashboardCard ToCommitmentDashboardCard(this ExtractedItem item)
    {
        var hint = item.Confidence >= 0.9
            ? "Высокая уверенность"
            : item.Confidence >= 0.75
                ? "Похоже на обещание"
                : "Нужна проверка";

        return new TodayModel.DashboardCard(
            item.Title,
            item.Summary,
            item.Person ?? item.SourceRoom,
            item.DueAt ?? item.ObservedAt,
            hint,
            BuildSearchQuery(item.Title, item.Summary, item.SourceRoom),
            item.Confidence);
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
