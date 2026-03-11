using Microsoft.AspNetCore.Authorization;
using SuperChat.Api.Security;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Api.Features.Auth;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/auth");

        group.MapPost("/magic-links", async (
            MagicLinkRequest request,
            IAuthFlowService authFlowService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["email"] = ["Email is required."]
                });
            }

            var result = await authFlowService.RequestMagicLinkAsync(request.Email, cancellationToken);
            return result.Accepted
                ? Results.Accepted(value: new MagicLinkResponse(result.Accepted, result.Message, result.DevelopmentLink))
                : Results.Problem(title: "Magic link request rejected", detail: result.Message, statusCode: StatusCodes.Status403Forbidden);
        });

        group.MapPost("/token-exchange", async (
            TokenExchangeRequest request,
            IAuthFlowService authFlowService,
            IApiSessionService apiSessionService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["token"] = ["Token is required."]
                });
            }

            var result = await authFlowService.VerifyAsync(request.Token, cancellationToken);
            if (!result.Accepted || result.User is null)
            {
                return Results.Problem(title: "Token exchange failed", detail: result.Message, statusCode: StatusCodes.Status400BadRequest);
            }

            var session = await apiSessionService.IssueAsync(result.User, cancellationToken);
            return Results.Ok(ToSessionResponse(result.User, session));
        });

        group.MapPost("/refresh", [Authorize(AuthenticationSchemes = ApiSessionAuthenticationHandler.SchemeName)] async (
            HttpContext httpContext,
            IApiSessionService apiSessionService,
            CancellationToken cancellationToken) =>
        {
            var user = await ResolveCurrentUserAsync(httpContext, apiSessionService, cancellationToken);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            if (!TryGetBearerToken(httpContext, out var currentToken))
            {
                return Results.Unauthorized();
            }

            await apiSessionService.RevokeAsync(currentToken, cancellationToken);
            var session = await apiSessionService.IssueAsync(user, cancellationToken);
            return Results.Ok(ToSessionResponse(user, session));
        });

        group.MapPost("/logout", [Authorize(AuthenticationSchemes = ApiSessionAuthenticationHandler.SchemeName)] async (
            HttpContext httpContext,
            IApiSessionService apiSessionService,
            CancellationToken cancellationToken) =>
        {
            if (TryGetBearerToken(httpContext, out var token))
            {
                await apiSessionService.RevokeAsync(token, cancellationToken);
            }

            return Results.NoContent();
        });

        return group;
    }

    private static SessionTokenResponse ToSessionResponse(AppUser user, ApiSession session)
    {
        return new SessionTokenResponse(
            AccessToken: session.Token,
            TokenType: "Bearer",
            ExpiresAt: session.ExpiresAt,
            User: new ApiUserResponse(user.Id, user.Email));
    }

    private static async Task<AppUser?> ResolveCurrentUserAsync(
        HttpContext httpContext,
        IApiSessionService apiSessionService,
        CancellationToken cancellationToken)
    {
        if (!TryGetBearerToken(httpContext, out var token))
        {
            return null;
        }

        return await apiSessionService.GetUserAsync(token, cancellationToken);
    }

    internal static bool TryGetBearerToken(HttpContext httpContext, out string token)
    {
        var headerValue = httpContext.Request.Headers.Authorization.ToString();
        const string bearerPrefix = "Bearer ";
        if (!headerValue.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            token = string.Empty;
            return false;
        }

        token = headerValue[bearerPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(token);
    }
}

public sealed record MagicLinkRequest(string Email);

public sealed record MagicLinkResponse(bool Accepted, string Message, Uri? DevelopmentLink);

public sealed record TokenExchangeRequest(string Token);

public sealed record SessionTokenResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    ApiUserResponse User);

public sealed record ApiUserResponse(Guid Id, string Email);
