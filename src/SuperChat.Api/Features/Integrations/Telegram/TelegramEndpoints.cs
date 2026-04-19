using Microsoft.AspNetCore.Authorization;
using SuperChat.Api.Features.Auth;
using SuperChat.Api.Security;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Contracts.Features.Integrations;
using SuperChat.Domain.Features.Integrations;

namespace SuperChat.Api.Features.Integrations.Telegram;

public static class TelegramEndpoints
{
    public static RouteGroupBuilder MapTelegramEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/integrations/telegram")
            .WithTags("Telegram")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = ApiSessionAuthenticationHandler.SchemeName });

        group.MapGet(string.Empty, async (
            HttpContext httpContext,
            IIntegrationConnectionService integrationConnectionService,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.GetRequiredUserId();
            var connection = await integrationConnectionService.GetStatusAsync(userId, IntegrationProvider.Telegram, cancellationToken);
            return Results.Ok(connection.ToTelegramConnectionResponse());
        });

        group.MapPost("/connect", async (
            HttpContext httpContext,
            IAuthFlowService authFlowService,
            IIntegrationConnectionService integrationConnectionService,
            CancellationToken cancellationToken) =>
        {
            var user = await authFlowService.FindUserAsync(httpContext.User.GetRequiredEmail(), cancellationToken);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var connection = await integrationConnectionService.StartAsync(user, IntegrationProvider.Telegram, cancellationToken);
            return Results.Ok(connection.ToTelegramConnectionResponse());
        });

        group.MapPost("/reconnect", async (
            HttpContext httpContext,
            IAuthFlowService authFlowService,
            IIntegrationConnectionService integrationConnectionService,
            CancellationToken cancellationToken) =>
        {
            var user = await authFlowService.FindUserAsync(httpContext.User.GetRequiredEmail(), cancellationToken);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var connection = await integrationConnectionService.ReconnectAsync(user, IntegrationProvider.Telegram, cancellationToken);
            return Results.Ok(connection.ToTelegramConnectionResponse());
        });

        group.MapPost("/login-input", async (
            HttpContext httpContext,
            IAuthFlowService authFlowService,
            IIntegrationConnectionService integrationConnectionService,
            TelegramLoginInputRequest request,
            CancellationToken cancellationToken) =>
        {
            var user = await authFlowService.FindUserAsync(httpContext.User.GetRequiredEmail(), cancellationToken);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(request.Input))
            {
                return Results.BadRequest("Input is required.");
            }

            var connection = await integrationConnectionService.SubmitLoginInputAsync(
                user, IntegrationProvider.Telegram, request.Input.Trim(), cancellationToken);
            return Results.Ok(connection.ToTelegramConnectionResponse());
        });

        group.MapDelete(string.Empty, async (
            HttpContext httpContext,
            IIntegrationConnectionService integrationConnectionService,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.GetRequiredUserId();
            await integrationConnectionService.DisconnectAsync(userId, IntegrationProvider.Telegram, cancellationToken);
            var connection = await integrationConnectionService.GetStatusAsync(userId, IntegrationProvider.Telegram, cancellationToken);
            return Results.Ok(connection.ToTelegramConnectionResponse());
        });

        return group;
    }
}

public sealed record TelegramLoginInputRequest(string Input);

public sealed record TelegramConnectionResponse(
    string State,
    string? ChatLoginStep,
    DateTimeOffset? LastSyncedAt,
    bool RequiresAction);
