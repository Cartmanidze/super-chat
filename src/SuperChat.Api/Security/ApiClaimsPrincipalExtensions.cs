using System.Security.Claims;

namespace SuperChat.Api.Security;

public static class ApiClaimsPrincipalExtensions
{
    public static Guid GetRequiredUserId(this ClaimsPrincipal user)
    {
        var rawValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return rawValue is null ? Guid.Empty : Guid.Parse(rawValue);
    }

    public static string GetRequiredEmail(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
    }
}
