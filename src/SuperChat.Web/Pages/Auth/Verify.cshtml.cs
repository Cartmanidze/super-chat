using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Web.Pages.Auth;

public sealed class VerifyModel(IAuthFlowService authFlowService) : PageModel
{
    public bool Success { get; private set; }

    public string StatusMessage { get; private set; } = "Checking your link...";

    public async Task<IActionResult> OnGetAsync(string token, CancellationToken cancellationToken)
    {
        var result = await authFlowService.VerifyAsync(token, cancellationToken);
        Success = result.Accepted;
        StatusMessage = result.Message;

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

        return RedirectToPage("/Connect/Telegram");
    }
}
