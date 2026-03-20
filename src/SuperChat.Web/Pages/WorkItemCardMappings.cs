using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;

namespace SuperChat.Web.Pages;

internal static class WorkItemCardMappings
{
    public static TodayModel.TodayCard ToWorkItemCard(this WorkItemCardViewModel card, string hint)
    {
        var timestamp = card.DueAt ?? card.ObservedAt;
        var searchQuery = BuildSearchQuery(card.Title, card.Summary, card.SourceRoom);

        return new TodayModel.TodayCard(
            card.Title,
            card.Summary,
            card.SourceRoom,
            timestamp,
            hint,
            searchQuery,
            card.Confidence);
    }

    public static TodayModel.TodayCard ToCommitmentWorkItemCard(this WorkItemRecord item)
    {
        var hint = item.Confidence >= 0.9
            ? "Высокая уверенность"
            : item.Confidence >= 0.75
                ? "Похоже на обещание"
                : "Нужна проверка";

        return new TodayModel.TodayCard(
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
