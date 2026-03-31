using SuperChat.Contracts.Features.Auth;
using SuperChat.Domain.Features.Auth;

namespace SuperChat.Api.Features.Auth;

internal static class AuthResponseMappings
{
    public static SendCodeResponse ToSendCodeResponse(this SendCodeResult result)
    {
        return new SendCodeResponse(result.Message);
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
