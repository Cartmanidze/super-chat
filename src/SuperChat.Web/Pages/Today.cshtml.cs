using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SuperChat.Contracts.Features.Integrations;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Integrations;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Web.Security;

namespace SuperChat.Web.Pages;

[Authorize]
public sealed class TodayModel(
    IDigestService digestService,
    IWorkItemService workItemService,
    IIntegrationConnectionService integrationConnectionService,
    IRequestWorkItemCommandService requestWorkItemCommandService,
    IActionItemWorkItemCommandService actionItemWorkItemCommandService,
    IEventWorkItemCommandService eventWorkItemCommandService,
    TimeProvider timeProvider) : PageModel
{
    private const string DefaultSectionId = "waiting";

    public string SelectedSectionId { get; private set; } = DefaultSectionId;

    public bool TelegramConnected { get; private set; }

    public TodaySummary Summary { get; private set; } = new(0, 0, 0, 0);

    public TodaySection WaitingSection { get; private set; } = TodaySection.Empty(
        "waiting",
        "Нужно ответить",
        "Точки, где кто-то ждет от вас следующий шаг.",
        "Сейчас нет явных мест, где кто-то ждет ответ.");

    public TodaySection CommitmentsSection { get; private set; } = TodaySection.Empty(
        "commitments",
        "Ты пообещал",
        "Фразы, где система увидела обещание или взятый на себя следующий шаг.",
        "Активных обещаний пока не выделилось.");

    public TodaySection MeetingsSection { get; private set; } = TodaySection.Empty(
        "meetings",
        "Скоро встречи",
        "Ближайшие встречи и временные договоренности, найденные в переписках.",
        "Ближайших встреч пока не видно.");

    public TodaySection TodayFocusSection { get; private set; } = TodaySection.Empty(
        "today",
        "Важно сегодня",
        "Самое важное на сегодня из текущих переписок.",
        "На сегодня пока ничего критичного не выделилось.");

    public TodaySection ActiveSection { get; private set; } = TodaySection.Empty(
        DefaultSectionId,
        "Нужно ответить",
        "Точки, где кто-то ждет от вас следующий шаг.",
        "Сейчас нет явных мест, где кто-то ждет ответ.");

    public IReadOnlyList<ResolvedHistoryCard> RecentResolvedItems { get; private set; } = [];

    public IReadOnlyList<TodaySection> Sections => [
        WaitingSection,
        CommitmentsSection,
        MeetingsSection,
        TodayFocusSection
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
        var allItems = await workItemService.GetForUserAsync(userId, cancellationToken);
        var commitments = allItems
            .Where(item => item.ResolvedAt is null)
            .Where(item => item.Kind == ExtractedItemKind.Commitment)
            .OrderByDescending(item => item.DueAt ?? item.ObservedAt)
            .Take(8)
            .ToList();
        RecentResolvedItems = ResolvedHistoryComposer.BuildRecentAutoResolved(
                allItems,
                timeProvider.GetUtcNow(),
                6)
            .Select(item => item.ToResolvedHistoryCard())
            .ToList();

        WaitingSection = new TodaySection(
            "waiting",
            "Нужно ответить",
            "Точки, где кто-то ждет от вас следующий шаг.",
            "Сейчас нет явных мест, где кто-то ждет ответ.",
            waitingCards.Select(card => card.ToWorkItemCard("Ожидает ответа")).ToList());

        CommitmentsSection = new TodaySection(
            "commitments",
            "Ты пообещал",
            "Фразы, где система увидела обещание или взятый на себя следующий шаг.",
            "Активных обещаний пока не выделилось.",
            commitments.Select(item => item.ToCommitmentWorkItemCard()).ToList());

        MeetingsSection = new TodaySection(
            "meetings",
            "Скоро встречи",
            "Ближайшие встречи и временные договоренности, найденные в переписках.",
            "Ближайших встреч пока не видно.",
            meetingCards.Select(card => card.ToWorkItemCard("Временной сигнал")).ToList());

        TodayFocusSection = new TodaySection(
            "today",
            "Важно сегодня",
            "Самое важное на сегодня из текущих переписок.",
            "На сегодня пока ничего критичного не выделилось.",
            todayCards.Select(card => card.ToWorkItemCard("Сегодня в фокусе")).ToList());

        Summary = new TodaySummary(
            WaitingSection.Items.Count,
            CommitmentsSection.Items.Count,
            MeetingsSection.Items.Count,
            TodayFocusSection.Items.Count);

        ActiveSection = ResolveActiveSection(SelectedSectionId);
        SelectedSectionId = ActiveSection.Id;
    }

    public async Task<IActionResult> OnPostCompleteAsync(
        Guid? id,
        WorkItemType? type,
        string? section,
        CancellationToken cancellationToken)
    {
        if (id is not null && type is not null)
        {
            await ExecuteActionAsync(id.Value, type.Value, complete: true, cancellationToken);
        }

        return RedirectToCurrentSection(section);
    }

    public async Task<IActionResult> OnPostDismissAsync(
        Guid? id,
        WorkItemType? type,
        string? section,
        CancellationToken cancellationToken)
    {
        if (id is not null && type is not null)
        {
            await ExecuteActionAsync(id.Value, type.Value, complete: false, cancellationToken);
        }

        return RedirectToCurrentSection(section);
    }

    private async Task ExecuteActionAsync(
        Guid id,
        WorkItemType type,
        bool complete,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        switch (type)
        {
            case WorkItemType.Request:
                if (complete)
                {
                    await requestWorkItemCommandService.CompleteAsync(userId, id, cancellationToken);
                }
                else
                {
                    await requestWorkItemCommandService.DismissAsync(userId, id, cancellationToken);
                }

                break;

            case WorkItemType.ActionItem:
                if (complete)
                {
                    await actionItemWorkItemCommandService.CompleteAsync(userId, id, cancellationToken);
                }
                else
                {
                    await actionItemWorkItemCommandService.DismissAsync(userId, id, cancellationToken);
                }

                break;

            case WorkItemType.Event:
                if (complete)
                {
                    await eventWorkItemCommandService.CompleteAsync(userId, id, cancellationToken);
                }
                else
                {
                    await eventWorkItemCommandService.DismissAsync(userId, id, cancellationToken);
                }

                break;
        }
    }

    private RedirectToPageResult RedirectToCurrentSection(string? section)
    {
        return RedirectToPage("/Today", new
        {
            section = NormalizeSectionId(section)
        });
    }

    private TodaySection ResolveActiveSection(string? selectedSectionId)
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

    public sealed record TodaySummary(
        int WaitingCount,
        int CommitmentCount,
        int MeetingCount,
        int TodayCount);

    public sealed record TodaySection(
        string Id,
        string Title,
        string Description,
        string EmptyText,
        IReadOnlyList<TodayCard> Items)
    {
        public static TodaySection Empty(string id, string title, string description, string emptyText)
        {
            return new TodaySection(id, title, description, emptyText, []);
        }
    }

    public sealed record TodayCard(
        Guid? Id,
        WorkItemType? Type,
        string Title,
        string Summary,
        string ChatLabel,
        DateTimeOffset Timestamp,
        string Hint,
        string SearchQuery,
        double Confidence)
    {
        public int ConfidencePercent => (int)Math.Round(Math.Clamp(Confidence, 0d, 1d) * 100, MidpointRounding.AwayFromZero);

        public bool SupportsActions => Id is not null && Type is not null;
    }
}
