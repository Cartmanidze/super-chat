using System.Security.Claims;
using SuperChat.Contracts.Features.Auth;

namespace SuperChat.Api.Security;

public static class ApiClaimsPrincipalExtensions
{
    public static Guid GetRequiredUserId(this ClaimsPrincipal user)
    {
        var rawValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            throw new InvalidSessionException(InvalidSessionFailureReason.MissingUserIdClaim);
        }

        if (!Guid.TryParse(rawValue, out var userId))
        {
            throw new InvalidSessionException(InvalidSessionFailureReason.MalformedUserIdClaim, rawValue);
        }

        if (userId == Guid.Empty)
        {
            throw new InvalidSessionException(InvalidSessionFailureReason.EmptyUserIdClaim, rawValue);
        }

        return userId;
    }

    public static string GetRequiredEmail(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
    }
}
