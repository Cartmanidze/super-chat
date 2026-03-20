using Microsoft.AspNetCore.Authorization;
using SuperChat.Api.Features.Auth;
using SuperChat.Api.Security;
using SuperChat.Domain.Features.Integrations;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Features.Integrations;
using SuperChat.Infrastructure.Features.Integrations.Matrix;

namespace SuperChat.Api.Features.Me;

public static class MeEndpoints
{
    public static RouteGroupBuilder MapMeEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/me", [Authorize(AuthenticationSchemes = ApiSessionAuthenticationHandler.SchemeName)] async (
            HttpContext httpContext,
            IIntegrationConnectionService integrationConnectionService,
            IMatrixProvisioningService matrixProvisioningService,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.GetRequiredUserId();
            var email = httpContext.User.GetRequiredEmail();
            var matrixIdentity = await matrixProvisioningService.GetIdentityAsync(userId, cancellationToken);
            var connection = await integrationConnectionService.GetStatusAsync(userId, IntegrationProvider.Telegram, cancellationToken);

            return Results.Ok(connection.ToMeResponse(userId, email, matrixIdentity?.MatrixUserId));
        });

        return api;
    }
}

public sealed record MeResponse(
    Guid Id,
    string Email,
    string? MatrixUserId,
    string TelegramState,
    DateTimeOffset? LastSyncedAt,
    bool RequiresTelegramAction);
