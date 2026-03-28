using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SuperChat.Domain.Features.Integrations;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Features.Auth;
using SuperChat.Infrastructure.Features.Integrations;
using SuperChat.Infrastructure.Features.Integrations.Matrix;
using SuperChat.Web.Security;

namespace SuperChat.Web.Pages.Connect;

[Authorize]
public sealed class TelegramModel(
    IAuthFlowService authFlowService,
    IIntegrationConnectionService integrationConnectionService,
    IMatrixProvisioningService matrixProvisioningService) : PageModel
{
    public IntegrationConnection Connection { get; private set; } = new(
        Guid.Empty,
        IntegrationProvider.Telegram,
        IntegrationProvider.Telegram.GetDefaultTransport(),
        IntegrationConnectionState.NotStarted,
        null,
        DateTimeOffset.UtcNow,
        null);

    public string? MatrixUserId { get; private set; }

    [BindProperty]
    public string? LoginInput { get; set; }

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

        await integrationConnectionService.StartAsync(user, IntegrationProvider.Telegram, cancellationToken);
        await LoadStateAsync(cancellationToken);
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
        await LoadStateAsync(cancellationToken);
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
        await LoadStateAsync(cancellationToken);
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

        Connection = await integrationConnectionService.GetStatusAsync(user.Id, IntegrationProvider.Telegram, cancellationToken);
        MatrixUserId = (await matrixProvisioningService.GetIdentityAsync(user.Id, cancellationToken))?.MatrixUserId;
    }
}
