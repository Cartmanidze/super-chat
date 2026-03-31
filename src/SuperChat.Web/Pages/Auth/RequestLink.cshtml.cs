using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Web.Localization;

namespace SuperChat.Web.Pages.Auth;

public sealed class RequestLinkModel(
    IAuthFlowService authFlowService,
    IUiTextService uiTextService,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public string Email { get; set; } = string.Empty;

    public string StatusMessage { get; private set; } = string.Empty;

    public bool CodeSent { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            StatusMessage = localizer["Auth.Email.Required"];
            return Page();
        }

        try
        {
            var result = await authFlowService.SendCodeAsync(Email, cancellationToken);
            StatusMessage = uiTextService.SendCodeStatusText(result.Status);
            CodeSent = result.Accepted;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            StatusMessage = localizer["Auth.Code.GenericError"];
        }

        return Page();
    }
}
