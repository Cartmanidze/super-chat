using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.State;
using SuperChat.Web.Security;

namespace SuperChat.Web.Pages.Settings;

[Authorize]
public sealed class ConnectionsModel(
    IAuthFlowService authFlowService,
    ITelegramConnectionService telegramConnectionService,
    SuperChatStore store) : PageModel
{
    public string Email { get; private set; } = string.Empty;

    public string MatrixUserId { get; private set; } = "pending";

    public string ConnectionState { get; private set; } = "NotStarted";

    public DateTimeOffset? LastSyncedAt { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostDisconnectAsync(CancellationToken cancellationToken)
    {
        var user = authFlowService.FindUser(User.GetEmail());
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
        var user = authFlowService.FindUser(Email);
        if (user is null)
        {
            return;
        }

        MatrixUserId = store.GetMatrixIdentity(user.Id)?.MatrixUserId ?? "pending";
        var connection = await telegramConnectionService.GetStatusAsync(user.Id, cancellationToken);
        ConnectionState = connection.State.ToString();
        LastSyncedAt = connection.LastSyncedAt;
    }
}
