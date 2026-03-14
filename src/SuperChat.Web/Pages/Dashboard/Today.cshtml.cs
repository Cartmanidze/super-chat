using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SuperChat.Domain.Model;
using SuperChat.Contracts.ViewModels;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Web.Security;

namespace SuperChat.Web.Pages.Dashboard;

[Authorize]
public sealed class TodayModel(
    IDigestService digestService,
    IExtractedItemService extractedItemService,
    IIntegrationConnectionService integrationConnectionService) : PageModel
{
    public bool TelegramConnected { get; private set; }

    public DashboardSummary Summary { get; private set; } = new(0, 0, 0, 0);

    public DashboardSection WaitingSection { get; private set; } = DashboardSection.Empty(
        "waiting",
        "Нужно ответить",
        "Точки, где кто-то ждёт от вас следующий шаг.",
        "Сейчас нет явных мест, где кто-то ждёт ответ.");

    public DashboardSection CommitmentsSection { get; private set; } = DashboardSection.Empty(
        "commitments",
        "Ты пообещал",
        "Фразы, где система увидела обещание или взятый на себя следующий шаг.",
        "Активных обещаний пока не выделилось.");

    public DashboardSection MeetingsSection { get; private set; } = DashboardSection.Empty(
        "meetings",
        "Скоро встречи",
        "Ближайшие встречи и временные договорённости, найденные в переписках.",
        "Ближайших встреч пока не видно.");

    public DashboardSection TodaySection { get; private set; } = DashboardSection.Empty(
        "today",
        "Важно сегодня",
        "Самое важное на сегодня из текущих переписок.",
        "На сегодня пока ничего критичного не выделилось.");

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var connection = await integrationConnectionService.GetStatusAsync(userId, IntegrationProvider.Telegram, cancellationToken);
        TelegramConnected = connection.State == IntegrationConnectionState.Connected;

        if (!TelegramConnected)
        {
            return;
        }

        var waitingCards = await digestService.GetWaitingAsync(userId, cancellationToken);
        var meetingCards = await digestService.GetMeetingsAsync(userId, cancellationToken);
        var todayCards = await digestService.GetTodayAsync(userId, cancellationToken);
        var commitments = (await extractedItemService.GetForUserAsync(userId, cancellationToken))
            .Where(item => item.Kind == ExtractedItemKind.Commitment)
            .OrderByDescending(item => item.DueAt ?? item.ObservedAt)
            .Take(8)
            .ToList();

        WaitingSection = new DashboardSection(
            "waiting",
            "Нужно ответить",
            "Точки, где кто-то ждёт от вас следующий шаг.",
            "Сейчас нет явных мест, где кто-то ждёт ответ.",
            waitingCards.Select(card => MapDigestCard(card, "Ожидает ответа")).ToList());

        CommitmentsSection = new DashboardSection(
            "commitments",
            "Ты пообещал",
            "Фразы, где система увидела обещание или взятый на себя следующий шаг.",
            "Активных обещаний пока не выделилось.",
            commitments.Select(MapCommitmentCard).ToList());

        MeetingsSection = new DashboardSection(
            "meetings",
            "Скоро встречи",
            "Ближайшие встречи и временные договорённости, найденные в переписках.",
            "Ближайших встреч пока не видно.",
            meetingCards.Select(card => MapDigestCard(card, "Временной сигнал")).ToList());

        TodaySection = new DashboardSection(
            "today",
            "Важно сегодня",
            "Самое важное на сегодня из текущих переписок.",
            "На сегодня пока ничего критичного не выделилось.",
            todayCards.Select(card => MapDigestCard(card, "Сегодня в фокусе")).ToList());

        Summary = new DashboardSummary(
            WaitingSection.Items.Count,
            CommitmentsSection.Items.Count,
            MeetingsSection.Items.Count,
            TodaySection.Items.Count);
    }

    private static DashboardCard MapDigestCard(DashboardCardViewModel card, string hint)
    {
        var timestamp = card.DueAt ?? card.ObservedAt;
        var searchQuery = BuildSearchQuery(card.Title, card.Summary, card.SourceRoom);

        return new DashboardCard(
            card.Title,
            card.Summary,
            card.SourceRoom,
            timestamp,
            hint,
            searchQuery);
    }

    private static DashboardCard MapCommitmentCard(ExtractedItem item)
    {
        var hint = item.Confidence >= 0.9
            ? "Высокая уверенность"
            : item.Confidence >= 0.75
                ? "Похоже на обещание"
                : "Нужна проверка";

        return new DashboardCard(
            item.Title,
            item.Summary,
            item.Person ?? item.SourceRoom,
            item.DueAt ?? item.ObservedAt,
            hint,
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

    public sealed record DashboardSummary(
        int WaitingCount,
        int CommitmentCount,
        int MeetingCount,
        int TodayCount);

    public sealed record DashboardSection(
        string Id,
        string Title,
        string Description,
        string EmptyText,
        IReadOnlyList<DashboardCard> Items)
    {
        public static DashboardSection Empty(string id, string title, string description, string emptyText)
        {
            return new DashboardSection(id, title, description, emptyText, []);
        }
    }

    public sealed record DashboardCard(
        string Title,
        string Summary,
        string ChatLabel,
        DateTimeOffset Timestamp,
        string Hint,
        string SearchQuery);
}
