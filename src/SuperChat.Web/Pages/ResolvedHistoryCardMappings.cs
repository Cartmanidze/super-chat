using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Web.Pages;

internal static class ResolvedHistoryCardMappings
{
    public static ResolvedHistoryCard ToResolvedHistoryCard(this WorkItemRecord item)
    {
        var resolvedAt = item.ResolvedAt ?? item.ObservedAt;
        var note = item.ToResolutionNote() ?? "Авто закрыто";

        return new ResolvedHistoryCard(
            item.Title,
            item.Summary,
            item.SourceRoom,
            resolvedAt,
            note,
            BuildSearchQuery(item.Title, item.Summary, item.SourceRoom));
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
