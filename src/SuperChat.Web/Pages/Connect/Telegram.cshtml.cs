using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Contracts.Features.Integrations;
using SuperChat.Contracts.Features.Integrations.Matrix;
using SuperChat.Domain.Features.Auth;
using SuperChat.Domain.Features.Integrations;
using SuperChat.Web.Localization;
using SuperChat.Web.Security;

namespace SuperChat.Web.Pages.Connect;

[Authorize]
public sealed class TelegramModel(
    IAuthFlowService authFlowService,
    IIntegrationConnectionService integrationConnectionService,
    IMatrixProvisioningService matrixProvisioningService,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    private const string AwaitingConnectionConfirmationToken = "pending";

    public IntegrationConnection Connection { get; private set; } = null!;

    public string? MatrixUserId { get; private set; }

    [BindProperty]
    public string? LoginInput { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? AwaitingConnectionConfirmation { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(cancellationToken);
        if (user is null)
        {
            return RedirectToPage("/Index");
        }

        await LoadStateAsync(user.Id, cancellationToken);
        ApplyConnectionFeedback();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(cancellationToken);
        if (user is null)
        {
            return RedirectToPage("/Index");
        }

        var connection = await integrationConnectionService.StartAsync(user, IntegrationProvider.Telegram, cancellationToken);
        return RedirectWithConnectionFeedback(connection, connection.ChatLoginStep);
    }

    public async Task<IActionResult> OnPostReconnectAsync(CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(cancellationToken);
        if (user is null)
        {
            return RedirectToPage("/Index");
        }

        var connection = await integrationConnectionService.ReconnectAsync(user, IntegrationProvider.Telegram, cancellationToken);
        return RedirectWithConnectionFeedback(connection, connection.ChatLoginStep);
    }

    public async Task<IActionResult> OnPostStartChatLoginAsync(CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(cancellationToken);
        if (user is null)
        {
            return RedirectToPage("/Index");
        }

        var connection = await integrationConnectionService.StartChatLoginAsync(
            user,
            IntegrationProvider.Telegram,
            cancellationToken);
        return RedirectWithConnectionFeedback(connection, connection.ChatLoginStep);
    }

    public async Task<IActionResult> OnPostSubmitLoginInputAsync(CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(cancellationToken);
        if (user is null)
        {
            return RedirectToPage("/Index");
        }

        await LoadStateAsync(user.Id, cancellationToken);
        var loginStep = Connection.ChatLoginStep;
        if (!TryBuildSubmittedInput(loginStep, out var submittedInput))
        {
            return Page();
        }

        var connection = await integrationConnectionService.SubmitLoginInputAsync(
            user,
            IntegrationProvider.Telegram,
            submittedInput,
            cancellationToken);

        return RedirectWithConnectionFeedback(connection, loginStep);
    }

    private async Task LoadStateAsync(Guid userId, CancellationToken cancellationToken)
    {
        Connection = await integrationConnectionService.GetStatusAsync(userId, IntegrationProvider.Telegram, cancellationToken);
        MatrixUserId = (await matrixProvisioningService.GetIdentityAsync(userId, cancellationToken))?.MatrixUserId;
    }

    private async Task<AppUser?> FindUserAsync(CancellationToken cancellationToken)
    {
        return await authFlowService.FindUserAsync(User.GetEmail(), cancellationToken);
    }

    private IActionResult RedirectWithConnectionFeedback(IntegrationConnection connection, string? loginStep)
    {
        StatusMessage = null;
        ErrorMessage = null;
        SuccessMessage = null;

        switch (connection.State)
        {
            case IntegrationConnectionState.Connected:
                AwaitingConnectionConfirmation = null;
                SuccessMessage = localizer["ConnectTelegram.SuccessConnected"];
                break;
            case IntegrationConnectionState.Error:
                AwaitingConnectionConfirmation = null;
                ErrorMessage = localizer["ConnectTelegram.Error.StartFailed"];
                break;
            case IntegrationConnectionState.RequiresSetup:
                AwaitingConnectionConfirmation = null;
                ErrorMessage = localizer["ConnectTelegram.Error.RequiresSetup"];
                break;
            default:
                AwaitingConnectionConfirmation = AwaitingConnectionConfirmationToken;
                StatusMessage = GetProgressMessage(loginStep);
                break;
        }

        return RedirectToPage();
    }

    private void ApplyConnectionFeedback()
    {
        if (!string.Equals(AwaitingConnectionConfirmation, AwaitingConnectionConfirmationToken, StringComparison.Ordinal))
        {
            return;
        }

        if (Connection.State == IntegrationConnectionState.Connected)
        {
            SuccessMessage = localizer["ConnectTelegram.SuccessConnected"];
            StatusMessage = null;
            ErrorMessage = null;
            AwaitingConnectionConfirmation = null;
            return;
        }

        if (Connection.State == IntegrationConnectionState.Error)
        {
            ErrorMessage ??= localizer["ConnectTelegram.Error.ConfirmationFailed"];
            AwaitingConnectionConfirmation = null;
            return;
        }

        if (Connection.State == IntegrationConnectionState.RequiresSetup)
        {
            ErrorMessage ??= localizer["ConnectTelegram.Error.RequiresSetup"];
            AwaitingConnectionConfirmation = null;
            return;
        }

        AwaitingConnectionConfirmation = AwaitingConnectionConfirmationToken;
    }

    private bool TryBuildSubmittedInput(string? loginStep, out string submittedInput)
    {
        submittedInput = string.Empty;
        var rawInput = LoginInput ?? string.Empty;

        if (string.Equals(loginStep, "password", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(rawInput))
            {
                ModelState.AddModelError(nameof(LoginInput), localizer["ConnectTelegram.Validation.PasswordRequired"]);
                return false;
            }

            submittedInput = rawInput;
            return true;
        }

        var trimmedInput = rawInput.Trim();
        if (string.IsNullOrWhiteSpace(trimmedInput))
        {
            ModelState.AddModelError(
                nameof(LoginInput),
                loginStep switch
                {
                    "code" => localizer["ConnectTelegram.Validation.CodeRequired"],
                    _ => localizer["ConnectTelegram.Validation.PhoneRequired"]
                });
            return false;
        }

        if (string.Equals(loginStep, "code", StringComparison.Ordinal))
        {
            var normalizedCode = new string(trimmedInput.Where(ch => !char.IsWhiteSpace(ch)).ToArray());
            if (normalizedCode.Length < 3)
            {
                ModelState.AddModelError(nameof(LoginInput), localizer["ConnectTelegram.Validation.CodeInvalid"]);
                return false;
            }

            submittedInput = normalizedCode;
            return true;
        }

        var digitCount = trimmedInput.Count(char.IsDigit);
        if (digitCount < 6)
        {
            ModelState.AddModelError(nameof(LoginInput), localizer["ConnectTelegram.Validation.PhoneInvalid"]);
            return false;
        }

        submittedInput = trimmedInput;
        return true;
    }

    private string GetProgressMessage(string? loginStep)
    {
        return loginStep switch
        {
            "phone" => localizer["ConnectTelegram.Progress.PhoneStepStarted"],
            "code" => localizer["ConnectTelegram.Progress.CodeSubmitted"],
            "password" => localizer["ConnectTelegram.Progress.PasswordSubmitted"],
            _ => localizer["ConnectTelegram.Progress.Started"]
        };
    }
}
