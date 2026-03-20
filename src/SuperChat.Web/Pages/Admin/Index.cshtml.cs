using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Admin;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Web.Security;

namespace SuperChat.Web.Pages.Admin;

public sealed class IndexModel(
    IPilotInviteAdminService pilotInviteAdminService,
    IOptions<PilotOptions> pilotOptions) : PageModel
{
    private TimeZoneInfo _displayTimeZone = TimeZoneInfo.Utc;

    [BindProperty]
    public string InviteEmail { get; set; } = string.Empty;

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public IReadOnlyList<AdminInviteViewModel> Invites { get; private set; } = [];

    public string DisplayTimeZoneId { get; private set; } = TimeZoneInfo.Utc.Id;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var guard = EnsureAdminUnlocked();
        if (guard is not null)
        {
            return guard;
        }

        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAddInviteAsync(CancellationToken cancellationToken)
    {
        var guard = EnsureAdminUnlocked();
        if (guard is not null)
        {
            return guard;
        }

        var result = await pilotInviteAdminService.AddInviteAsync(InviteEmail, User.GetEmail(), cancellationToken);
        if (result.Succeeded)
        {
            StatusMessage = result.Message;
        }
        else
        {
            ErrorMessage = result.Message;
        }

        return RedirectToPage();
    }

    public string FormatTimestamp(DateTimeOffset? value)
    {
        if (value is null)
        {
            return "Еще не было";
        }

        return TimeZoneInfo.ConvertTime(value.Value, _displayTimeZone).ToString("dd.MM.yyyy HH:mm:ss");
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        _displayTimeZone = ResolveTimeZone(pilotOptions.Value.TodayTimeZoneId);
        DisplayTimeZoneId = _displayTimeZone.Id;
        Invites = await pilotInviteAdminService.GetInvitesAsync(cancellationToken);
    }

    private IActionResult? EnsureAdminUnlocked()
    {
        if (!User.IsConfiguredAdmin(pilotOptions.Value))
        {
            return RedirectToPage("/Index");
        }

        if (User.HasAdminAccess(pilotOptions.Value))
        {
            return null;
        }

        return RedirectToPage("/Admin/Unlock", new { returnUrl = "/admin" });
    }

    private static TimeZoneInfo ResolveTimeZone(string configuredTimeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(configuredTimeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(configuredTimeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}
