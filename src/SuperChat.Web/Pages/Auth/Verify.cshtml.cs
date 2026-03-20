using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Features.Auth;
using SuperChat.Web.Localization;

namespace SuperChat.Web.Pages.Auth;

public sealed class VerifyModel(
    IAuthFlowService authFlowService,
    IStringLocalizer<SharedResource> localizer,
    IUiTextService uiTextService) : PageModel
{
    public bool Success { get; private set; }

    public string StatusMessage { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string token, CancellationToken cancellationToken)
    {
        StatusMessage = localizer["Auth.Verify.Checking"];
        var result = await authFlowService.VerifyAsync(token, cancellationToken);
        Success = result.Accepted;
        StatusMessage = uiTextService.AuthVerificationStatusText(result.Status);

        if (!result.Accepted || result.User is null)
        {
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.User.Id.ToString()),
            new(ClaimTypes.Email, result.User.Email),
            new(ClaimTypes.Name, result.User.Email)
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return RedirectToPage("/Index");
    }
}
