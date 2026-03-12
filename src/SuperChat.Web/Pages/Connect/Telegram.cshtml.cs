using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Web.Security;

namespace SuperChat.Web.Pages.Connect;

[Authorize]
public sealed class TelegramModel(
    IAuthFlowService authFlowService,
    ITelegramConnectionService telegramConnectionService,
    IMatrixProvisioningService matrixProvisioningService) : PageModel
{
    public TelegramConnection Connection { get; private set; } = new(Guid.Empty, TelegramConnectionState.NotStarted, null, DateTimeOffset.UtcNow, null);

    public string? MatrixUserId { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadStateAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var user = await authFlowService.FindUserAsync(User.GetEmail(), cancellationToken);
        if (user is null)
        {
            return RedirectToPage("/Index");
        }

        await telegramConnectionService.StartAsync(user, cancellationToken);
        await LoadStateAsync(cancellationToken);
        return Page();
    }

    private async Task LoadStateAsync(CancellationToken cancellationToken)
    {
        var user = await authFlowService.FindUserAsync(User.GetEmail(), cancellationToken);
        if (user is null)
        {
            return;
        }

        Connection = await telegramConnectionService.GetStatusAsync(user.Id, cancellationToken);
        MatrixUserId = (await matrixProvisioningService.GetIdentityAsync(user.Id, cancellationToken))?.MatrixUserId;
    }
}
