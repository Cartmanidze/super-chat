using System.Security.Claims;
using SuperChat.Contracts.Features.Auth;

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

    public static bool IsConfiguredAdmin(this ClaimsPrincipal user, PilotOptions options)
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

    public static bool HasUnlockedAdminSession(this ClaimsPrincipal user)
    {
        if (!(user.Identity?.IsAuthenticated ?? false))
        {
            return false;
        }

        return user.HasClaim(AdminClaimTypes.AdminUnlocked, AdminClaimTypes.TrueValue);
    }

    public static bool HasAdminAccess(this ClaimsPrincipal user, PilotOptions options)
    {
        return user.IsConfiguredAdmin(options) && user.HasUnlockedAdminSession();
    }

    public static ClaimsPrincipal WithUnlockedAdminSession(this ClaimsPrincipal user)
    {
        var principal = new ClaimsPrincipal();

        foreach (var identity in user.Identities)
        {
            var claims = identity.Claims
                .Where(claim => !string.Equals(claim.Type, AdminClaimTypes.AdminUnlocked, StringComparison.Ordinal))
                .ToList();

            if (identity.IsAuthenticated)
            {
                claims.Add(new Claim(AdminClaimTypes.AdminUnlocked, AdminClaimTypes.TrueValue));
            }

            principal.AddIdentity(new ClaimsIdentity(claims, identity.AuthenticationType, identity.NameClaimType, identity.RoleClaimType));
        }

        return principal;
    }
}
