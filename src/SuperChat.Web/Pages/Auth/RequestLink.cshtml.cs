using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Features.Auth;
using SuperChat.Web.Localization;

namespace SuperChat.Web.Pages.Auth;

public sealed class RequestLinkModel(
    IAuthFlowService authFlowService,
    IUiTextService uiTextService) : PageModel
{
    [BindProperty]
    public string Email { get; set; } = string.Empty;

    public string StatusMessage { get; private set; } = string.Empty;

    public Uri? DevelopmentLink { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var result = await authFlowService.RequestMagicLinkAsync(Email, cancellationToken);
        StatusMessage = uiTextService.MagicLinkRequestStatusText(result.Status);
        DevelopmentLink = result.DevelopmentLink;
        return Page();
    }
}
