using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Web.Security;

namespace SuperChat.Web.Pages.Admin;

[Authorize]
public sealed class UnlockModel(
    IAdminPasswordService adminPasswordService,
    IOptions<PilotOptions> pilotOptions) : PageModel
{
    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; private set; }

    public bool PasswordConfigured => adminPasswordService.IsConfigured;

    public IActionResult OnGet()
    {
        if (!User.IsConfiguredAdmin(pilotOptions.Value))
        {
            return RedirectToPage("/Index");
        }

        if (User.HasAdminAccess(pilotOptions.Value))
        {
            return LocalRedirect(ResolveReturnUrl());
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!User.IsConfiguredAdmin(pilotOptions.Value))
        {
            return RedirectToPage("/Index");
        }

        if (!adminPasswordService.IsConfigured)
        {
            ErrorMessage = "Пароль админки не настроен.";
            return Page();
        }

        if (!adminPasswordService.Verify(Password))
        {
            ErrorMessage = "Неверный пароль.";
            return Page();
        }

        var authenticationResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = User.WithUnlockedAdminSession();
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            authenticationResult.Properties ?? new AuthenticationProperties());

        return LocalRedirect(ResolveReturnUrl());
    }

    private string ResolveReturnUrl()
    {
        return IsSafeLocalReturnUrl(ReturnUrl) ? ReturnUrl! : "/admin";
    }

    private static bool IsSafeLocalReturnUrl(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl) &&
               returnUrl.StartsWith("/", StringComparison.Ordinal) &&
               !returnUrl.StartsWith("//", StringComparison.Ordinal) &&
               !returnUrl.StartsWith("/\\", StringComparison.Ordinal);
    }
}
