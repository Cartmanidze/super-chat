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
            ITelegramConnectionService telegramConnectionService,
            IMatrixProvisioningService matrixProvisioningService,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.GetRequiredUserId();
            var connection = await telegramConnectionService.GetStatusAsync(userId, cancellationToken);
            var matrixIdentity = await matrixProvisioningService.GetIdentityAsync(userId, cancellationToken);
            return Results.Ok(ToResponse(connection, matrixIdentity?.MatrixUserId));
        });

        group.MapPost("/connect", async (
            HttpContext httpContext,
            IAuthFlowService authFlowService,
            ITelegramConnectionService telegramConnectionService,
            IMatrixProvisioningService matrixProvisioningService,
            CancellationToken cancellationToken) =>
        {
            var user = await authFlowService.FindUserAsync(httpContext.User.GetRequiredEmail(), cancellationToken);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var connection = await telegramConnectionService.StartAsync(user, cancellationToken);
            var matrixIdentity = await matrixProvisioningService.GetIdentityAsync(user.Id, cancellationToken);
            return Results.Ok(ToResponse(connection, matrixIdentity?.MatrixUserId));
        });

        group.MapDelete(string.Empty, async (
            HttpContext httpContext,
            ITelegramConnectionService telegramConnectionService,
            IMatrixProvisioningService matrixProvisioningService,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.GetRequiredUserId();
            await telegramConnectionService.DisconnectAsync(userId, cancellationToken);
            var connection = await telegramConnectionService.GetStatusAsync(userId, cancellationToken);
            var matrixIdentity = await matrixProvisioningService.GetIdentityAsync(userId, cancellationToken);
            return Results.Ok(ToResponse(connection, matrixIdentity?.MatrixUserId));
        });

        return group;
    }

    private static TelegramConnectionResponse ToResponse(TelegramConnection connection, string? matrixUserId)
    {
        return new TelegramConnectionResponse(
            State: connection.State.ToString(),
            MatrixUserId: matrixUserId,
            WebLoginUrl: connection.WebLoginUrl,
            LastSyncedAt: connection.LastSyncedAt,
            RequiresAction: connection.State is not TelegramConnectionState.Connected);
    }
}

public sealed record TelegramConnectionResponse(
    string State,
    string? MatrixUserId,
    Uri? WebLoginUrl,
    DateTimeOffset? LastSyncedAt,
    bool RequiresAction);
