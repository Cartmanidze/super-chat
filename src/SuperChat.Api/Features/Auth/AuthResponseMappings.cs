using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Api.Features.Auth;

internal static class AuthResponseMappings
{
    public static MagicLinkResponse ToMagicLinkResponse(this MagicLinkRequestResult result)
    {
        return new MagicLinkResponse(result.Accepted, result.Message, result.DevelopmentLink);
    }

    public static SessionTokenResponse ToSessionTokenResponse(this ApiSession session, AppUser user)
    {
        return new SessionTokenResponse(
            AccessToken: session.Token,
            TokenType: "Bearer",
            ExpiresAt: session.ExpiresAt,
            User: user.ToApiUserResponse());
    }

    public static ApiUserResponse ToApiUserResponse(this AppUser user)
    {
        return new ApiUserResponse(user.Id, user.Email);
    }
}
