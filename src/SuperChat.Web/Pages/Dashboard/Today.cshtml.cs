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
    private const string DefaultSectionId = "waiting";

    public string SelectedSectionId { get; private set; } = DefaultSectionId;

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

    public DashboardSection ActiveSection { get; private set; } = DashboardSection.Empty(
        DefaultSectionId,
        "Нужно ответить",
        "Точки, где кто-то ждёт от вас следующий шаг.",
        "Сейчас нет явных мест, где кто-то ждёт ответ.");

    public IReadOnlyList<DashboardSection> Sections => [
        WaitingSection,
        CommitmentsSection,
        MeetingsSection,
        TodaySection
    ];

    public async Task OnGetAsync(string? section, CancellationToken cancellationToken)
    {
        SelectedSectionId = NormalizeSectionId(section);

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
            waitingCards.Select(card => card.ToDashboardCard("Ожидает ответа")).ToList());

        CommitmentsSection = new DashboardSection(
            "commitments",
            "Ты пообещал",
            "Фразы, где система увидела обещание или взятый на себя следующий шаг.",
            "Активных обещаний пока не выделилось.",
            commitments.Select(item => item.ToCommitmentDashboardCard()).ToList());

        MeetingsSection = new DashboardSection(
            "meetings",
            "Скоро встречи",
            "Ближайшие встречи и временные договорённости, найденные в переписках.",
            "Ближайших встреч пока не видно.",
            meetingCards.Select(card => card.ToDashboardCard("Временной сигнал")).ToList());

        TodaySection = new DashboardSection(
            "today",
            "Важно сегодня",
            "Самое важное на сегодня из текущих переписок.",
            "На сегодня пока ничего критичного не выделилось.",
            todayCards.Select(card => card.ToDashboardCard("Сегодня в фокусе")).ToList());

        Summary = new DashboardSummary(
            WaitingSection.Items.Count,
            CommitmentsSection.Items.Count,
            MeetingsSection.Items.Count,
            TodaySection.Items.Count);

        ActiveSection = ResolveActiveSection(SelectedSectionId);
        SelectedSectionId = ActiveSection.Id;
    }

    private DashboardSection ResolveActiveSection(string? selectedSectionId)
    {
        return Sections.FirstOrDefault(section =>
                   string.Equals(section.Id, selectedSectionId, StringComparison.OrdinalIgnoreCase))
               ?? WaitingSection;
    }

    private static string NormalizeSectionId(string? sectionId)
    {
        return string.IsNullOrWhiteSpace(sectionId)
            ? DefaultSectionId
            : sectionId.Trim().ToLowerInvariant();
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
        string SearchQuery,
        double Confidence)
    {
        public int ConfidencePercent => (int)Math.Round(Math.Clamp(Confidence, 0d, 1d) * 100, MidpointRounding.AwayFromZero);
    }
}
