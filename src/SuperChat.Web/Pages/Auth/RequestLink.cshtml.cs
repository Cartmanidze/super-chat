using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Web.Pages.Auth;

public sealed class RequestLinkModel(IAuthFlowService authFlowService) : PageModel
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
        StatusMessage = result.Message;
        DevelopmentLink = result.DevelopmentLink;
        return Page();
    }
}
