using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SuperChat.Contracts.Features.Integrations;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Integrations;
using SuperChat.Web.Security;

namespace SuperChat.Web.Pages;

[Authorize]
public sealed class TodayModel(
    IDigestService digestService,
    IIntegrationConnectionService integrationConnectionService,
    IMeetingWorkItemCommandService meetingWorkItemCommandService) : PageModel
{
    public bool TelegramConnected { get; private set; }

    public MeetingsSummary Summary { get; private set; } = new(0);

    public TodaySection MeetingsSection { get; private set; } = TodaySection.Empty(
        "Ближайшие встречи",
        "Только будущие встречи и договоренности, которые уже можно показать как рабочий план.",
        "Ближайших встреч пока не видно.");

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var connection = await integrationConnectionService.GetStatusAsync(userId, IntegrationProvider.Telegram, cancellationToken);
        TelegramConnected = connection.State == IntegrationConnectionState.Connected;

        if (!TelegramConnected)
        {
            return;
        }

        var meetingCards = await digestService.GetMeetingsAsync(userId, cancellationToken);
        MeetingsSection = new TodaySection(
            "Ближайшие встречи",
            "Только будущие встречи и договоренности, которые уже можно показать как рабочий план.",
            "Ближайших встреч пока не видно.",
            meetingCards.Select(card => card.ToWorkItemCard("Скоро")).ToList());

        Summary = new MeetingsSummary(MeetingsSection.Items.Count);
    }

    public async Task<IActionResult> OnPostCompleteAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id is not null)
        {
            await meetingWorkItemCommandService.CompleteAsync(User.GetUserId(), id.Value, cancellationToken);
        }

        return RedirectToPage("/Today");
    }

    public async Task<IActionResult> OnPostDismissAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id is not null)
        {
            await meetingWorkItemCommandService.DismissAsync(User.GetUserId(), id.Value, cancellationToken);
        }

        return RedirectToPage("/Today");
    }

    public sealed record MeetingsSummary(int MeetingCount);

    public sealed record TodaySection(
        string Title,
        string Description,
        string EmptyText,
        IReadOnlyList<TodayCard> Items)
    {
        public static TodaySection Empty(string title, string description, string emptyText)
        {
            return new TodaySection(title, description, emptyText, []);
        }
    }

    public sealed record TodayCard(
        Guid? Id,
        string Title,
        string Summary,
        string ChatLabel,
        DateTimeOffset Timestamp,
        string Hint,
        string SearchQuery,
        double Confidence)
    {
        public int ConfidencePercent => (int)Math.Round(Math.Clamp(Confidence, 0d, 1d) * 100, MidpointRounding.AwayFromZero);

        public bool SupportsActions => Id is not null;
    }
}
