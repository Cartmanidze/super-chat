using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Web.Security;

namespace SuperChat.Web.Pages.Settings;

[Authorize]
public sealed class ConnectionsModel(
    IAuthFlowService authFlowService,
    ITelegramConnectionService telegramConnectionService,
    IMatrixProvisioningService matrixProvisioningService) : PageModel
{
    public string Email { get; private set; } = string.Empty;

    public string? MatrixUserId { get; private set; }

    public TelegramConnectionState ConnectionState { get; private set; } = TelegramConnectionState.NotStarted;

    public DateTimeOffset? LastSyncedAt { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostDisconnectAsync(CancellationToken cancellationToken)
    {
        var user = await authFlowService.FindUserAsync(User.GetEmail(), cancellationToken);
        if (user is null)
        {
            return RedirectToPage("/Index");
        }

        await telegramConnectionService.DisconnectAsync(user.Id, cancellationToken);
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Email = User.GetEmail();
        var user = await authFlowService.FindUserAsync(Email, cancellationToken);
        if (user is null)
        {
            return;
        }

        MatrixUserId = (await matrixProvisioningService.GetIdentityAsync(user.Id, cancellationToken))?.MatrixUserId;
        var connection = await telegramConnectionService.GetStatusAsync(user.Id, cancellationToken);
        ConnectionState = connection.State;
        LastSyncedAt = connection.LastSyncedAt;
    }
}
