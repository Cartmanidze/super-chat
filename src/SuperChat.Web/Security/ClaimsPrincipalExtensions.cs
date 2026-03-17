using System.Security.Claims;
using SuperChat.Contracts.Configuration;

namespace SuperChat.Web.Security;

public static class ClaimsPrincipalExtensions
{
    public static string GetEmail(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
    }

    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return value is null ? Guid.Empty : Guid.Parse(value);
    }

    public static bool IsAdmin(this ClaimsPrincipal user, PilotOptions options)
    {
        if (!(user.Identity?.IsAuthenticated ?? false))
        {
            return false;
        }

        var email = user.GetEmail();
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        return options.AdminEmails.Any(candidate =>
            !string.IsNullOrWhiteSpace(candidate) &&
            string.Equals(candidate.Trim(), email, StringComparison.OrdinalIgnoreCase));
    }
}
