using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Web.Localization;

namespace SuperChat.Web.Pages.Auth;

public sealed class VerifyModel(
    IAuthFlowService authFlowService,
    IStringLocalizer<SharedResource> localizer,
    IUiTextService uiTextService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Code { get; set; } = string.Empty;

    public bool Success { get; private set; }

    public string StatusMessage { get; private set; } = string.Empty;

    public void OnGet()
    {
        StatusMessage = localizer["Auth.Verify.EnterCode"];
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Code))
        {
            StatusMessage = localizer["Auth.Verify.EnterCode"];
            return Page();
        }

        AuthVerificationResult result;
        try
        {
            result = await authFlowService.VerifyCodeAsync(Email, Code, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            StatusMessage = localizer["Auth.Code.GenericError"];
            return Page();
        }

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
