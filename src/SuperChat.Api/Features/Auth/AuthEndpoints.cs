using Microsoft.AspNetCore.Authorization;
using SuperChat.Api.Validation;
using SuperChat.Api.Security;
using SuperChat.Domain.Features.Auth;
using SuperChat.Contracts.Features.Auth;

namespace SuperChat.Api.Features.Auth;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/auth")
            .WithTags("Auth");

        group.MapPost("/send-code", async (
            SendCodeRequest request,
            IAuthFlowService authFlowService,
            CancellationToken cancellationToken) =>
        {
            var result = await authFlowService.SendCodeAsync(request.Email, cancellationToken);
            if (result.Accepted)
                return Results.Accepted(value: result.ToSendCodeResponse());

            var statusCode = result.Status switch
            {
                SendCodeStatus.TooManyRequests => StatusCodes.Status429TooManyRequests,
                SendCodeStatus.DeliveryFailed => StatusCodes.Status502BadGateway,
                _ => StatusCodes.Status403Forbidden
            };
            return Results.Problem(title: "Code request rejected", detail: result.Message, statusCode: statusCode);
        })
        .ValidateRequest<SendCodeRequest>();

        group.MapPost("/verify-code", async (
            VerifyCodeRequest request,
            IAuthFlowService authFlowService,
            IApiSessionService apiSessionService,
            CancellationToken cancellationToken) =>
        {
            var result = await authFlowService.VerifyCodeAsync(request.Email, request.Code, cancellationToken);
            if (!result.Accepted || result.User is null)
            {
                var statusCode = result.Status == AuthVerificationStatus.TooManyAttempts
                    ? StatusCodes.Status429TooManyRequests
                    : StatusCodes.Status400BadRequest;
                return Results.Problem(title: "Code verification failed", detail: result.Message, statusCode: statusCode);
            }

            var session = await apiSessionService.IssueAsync(result.User, cancellationToken);
            return Results.Ok(session.ToSessionTokenResponse(result.User));
        })
        .ValidateRequest<VerifyCodeRequest>();

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
            return Results.Ok(session.ToSessionTokenResponse(user));
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

public sealed record SendCodeRequest(string Email);

public sealed record SendCodeResponse(string Message);

public sealed record VerifyCodeRequest(string Email, string Code);

public sealed record SessionTokenResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    ApiUserResponse User);

public sealed record ApiUserResponse(Guid Id, string Email);
