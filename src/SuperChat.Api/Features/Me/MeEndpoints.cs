using Microsoft.AspNetCore.Authorization;
using SuperChat.Api.Features.Auth;
using SuperChat.Api.Security;
using SuperChat.Contracts.Features.Integrations;
using SuperChat.Domain.Features.Integrations;

namespace SuperChat.Api.Features.Me;

public static class MeEndpoints
{
    public static RouteGroupBuilder MapMeEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/me", [Authorize(AuthenticationSchemes = ApiSessionAuthenticationHandler.SchemeName)] async (
            HttpContext httpContext,
            IIntegrationConnectionService integrationConnectionService,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.GetRequiredUserId();
            var email = httpContext.User.GetRequiredEmail();
            var connection = await integrationConnectionService.GetStatusAsync(userId, IntegrationProvider.Telegram, cancellationToken);

            return Results.Ok(connection.ToMeResponse(userId, email));
        })
        .WithTags("Me");

        return api;
    }
}

public sealed record MeResponse(
    Guid Id,
    string Email,
    string TelegramState,
    DateTimeOffset? LastSyncedAt,
    bool RequiresTelegramAction);
