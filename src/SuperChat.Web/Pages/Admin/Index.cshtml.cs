using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Contracts.Features.Admin;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Web.Security;

namespace SuperChat.Web.Pages.Admin;

[Authorize(Policy = AdminAuthorizationPolicy.PolicyName)]
public sealed class IndexModel(
    IPilotInviteAdminService pilotInviteAdminService,
    IWorkerRuntimeMonitor workerRuntimeMonitor,
    IOptions<PilotOptions> pilotOptions) : PageModel
{
    private TimeZoneInfo _displayTimeZone = TimeZoneInfo.Utc;

    [BindProperty]
    public string InviteEmail { get; set; } = string.Empty;

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public IReadOnlyList<WorkerRuntimeStatusViewModel> WorkerStatuses { get; private set; } = [];

    public IReadOnlyList<AdminInviteViewModel> Invites { get; private set; } = [];

    public string DisplayTimeZoneId { get; private set; } = TimeZoneInfo.Utc.Id;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAddInviteAsync(CancellationToken cancellationToken)
    {
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

    public string DescribeState(WorkerRuntimeState state)
    {
        return state switch
        {
            WorkerRuntimeState.NotStarted => "Не запускался",
            WorkerRuntimeState.Running => "Выполняется",
            WorkerRuntimeState.Succeeded => "Успешно",
            WorkerRuntimeState.Failed => "Ошибка",
            WorkerRuntimeState.Disabled => "Отключён",
            _ => state.ToString()
        };
    }

    public string GetStateBadgeCss(WorkerRuntimeState state)
    {
        return state switch
        {
            WorkerRuntimeState.Succeeded => "status-success",
            WorkerRuntimeState.Running => "status-warning",
            WorkerRuntimeState.Failed => "status-danger",
            WorkerRuntimeState.Disabled => "status-neutral",
            _ => "status-neutral"
        };
    }

    public string FormatTimestamp(DateTimeOffset? value)
    {
        if (value is null)
        {
            return "Ещё не было";
        }

        return TimeZoneInfo.ConvertTime(value.Value, _displayTimeZone).ToString("dd.MM.yyyy HH:mm:ss");
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        _displayTimeZone = ResolveTimeZone(pilotOptions.Value.TodayTimeZoneId);
        DisplayTimeZoneId = _displayTimeZone.Id;
        WorkerStatuses = workerRuntimeMonitor.GetStatuses();
        Invites = await pilotInviteAdminService.GetInvitesAsync(cancellationToken);
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
