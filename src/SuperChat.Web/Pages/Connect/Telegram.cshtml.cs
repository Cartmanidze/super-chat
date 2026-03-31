using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Contracts.Features.Integrations;
using SuperChat.Contracts.Features.Integrations.Matrix;
using SuperChat.Domain.Features.Integrations;
using SuperChat.Web.Security;

namespace SuperChat.Web.Pages.Connect;

[Authorize]
public sealed class TelegramModel(
    IAuthFlowService authFlowService,
    IIntegrationConnectionService integrationConnectionService,
    IMatrixProvisioningService matrixProvisioningService) : PageModel
{
    public IntegrationConnection Connection { get; private set; } = null!;

    public string? MatrixUserId { get; private set; }

    [BindProperty]
    public string? LoginInput { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var user = await authFlowService.FindUserAsync(User.GetEmail(), cancellationToken);
        if (user is null)
        {
            return RedirectToPage("/Index");
        }

        await LoadStateAsync(user.Id, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var user = await authFlowService.FindUserAsync(User.GetEmail(), cancellationToken);
        if (user is null)
        {
            return RedirectToPage("/Index");
        }

        await integrationConnectionService.StartAsync(user, IntegrationProvider.Telegram, cancellationToken);
        await LoadStateAsync(user.Id, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostReconnectAsync(CancellationToken cancellationToken)
    {
        var user = await authFlowService.FindUserAsync(User.GetEmail(), cancellationToken);
        if (user is null)
        {
            return RedirectToPage("/Index");
        }

        await integrationConnectionService.ReconnectAsync(user, IntegrationProvider.Telegram, cancellationToken);
        await LoadStateAsync(user.Id, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostStartChatLoginAsync(CancellationToken cancellationToken)
    {
        var user = await authFlowService.FindUserAsync(User.GetEmail(), cancellationToken);
        if (user is null)
        {
            return RedirectToPage("/Index");
        }

        await integrationConnectionService.StartChatLoginAsync(user, IntegrationProvider.Telegram, cancellationToken);
        await LoadStateAsync(user.Id, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSubmitLoginInputAsync(CancellationToken cancellationToken)
    {
        var user = await authFlowService.FindUserAsync(User.GetEmail(), cancellationToken);
        if (user is null)
        {
            return RedirectToPage("/Index");
        }

        if (!string.IsNullOrWhiteSpace(LoginInput))
        {
            await integrationConnectionService.SubmitLoginInputAsync(
                user, IntegrationProvider.Telegram, LoginInput.Trim(), cancellationToken);
        }

        await LoadStateAsync(user.Id, cancellationToken);
        return Page();
    }

    private async Task LoadStateAsync(Guid userId, CancellationToken cancellationToken)
    {
        Connection = await integrationConnectionService.GetStatusAsync(userId, IntegrationProvider.Telegram, cancellationToken);
        MatrixUserId = (await matrixProvisioningService.GetIdentityAsync(userId, cancellationToken))?.MatrixUserId;
    }
}
