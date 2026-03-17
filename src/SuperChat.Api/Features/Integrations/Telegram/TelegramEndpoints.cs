using Microsoft.AspNetCore.Authorization;
using SuperChat.Api.Features.Auth;
using SuperChat.Api.Security;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Api.Features.Integrations.Telegram;

public static class TelegramEndpoints
{
    public static RouteGroupBuilder MapTelegramEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/integrations/telegram")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = ApiSessionAuthenticationHandler.SchemeName });

        group.MapGet(string.Empty, async (
            HttpContext httpContext,
            IIntegrationConnectionService integrationConnectionService,
            IMatrixProvisioningService matrixProvisioningService,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.GetRequiredUserId();
            var connection = await integrationConnectionService.GetStatusAsync(userId, IntegrationProvider.Telegram, cancellationToken);
            var matrixIdentity = await matrixProvisioningService.GetIdentityAsync(userId, cancellationToken);
            return Results.Ok(connection.ToTelegramConnectionResponse(matrixIdentity?.MatrixUserId));
        });

        group.MapPost("/connect", async (
            HttpContext httpContext,
            IAuthFlowService authFlowService,
            IIntegrationConnectionService integrationConnectionService,
            IMatrixProvisioningService matrixProvisioningService,
            CancellationToken cancellationToken) =>
        {
            var user = await authFlowService.FindUserAsync(httpContext.User.GetRequiredEmail(), cancellationToken);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var connection = await integrationConnectionService.StartAsync(user, IntegrationProvider.Telegram, cancellationToken);
            var matrixIdentity = await matrixProvisioningService.GetIdentityAsync(user.Id, cancellationToken);
            return Results.Ok(connection.ToTelegramConnectionResponse(matrixIdentity?.MatrixUserId));
        });

        group.MapDelete(string.Empty, async (
            HttpContext httpContext,
            IIntegrationConnectionService integrationConnectionService,
            IMatrixProvisioningService matrixProvisioningService,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.GetRequiredUserId();
            await integrationConnectionService.DisconnectAsync(userId, IntegrationProvider.Telegram, cancellationToken);
            var connection = await integrationConnectionService.GetStatusAsync(userId, IntegrationProvider.Telegram, cancellationToken);
            var matrixIdentity = await matrixProvisioningService.GetIdentityAsync(userId, cancellationToken);
            return Results.Ok(connection.ToTelegramConnectionResponse(matrixIdentity?.MatrixUserId));
        });

        return group;
    }
}

public sealed record TelegramConnectionResponse(
    string State,
    string? MatrixUserId,
    Uri? WebLoginUrl,
    DateTimeOffset? LastSyncedAt,
    bool RequiresAction);
