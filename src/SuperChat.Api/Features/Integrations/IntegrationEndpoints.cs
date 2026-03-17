using Microsoft.AspNetCore.Authorization;
using SuperChat.Api.Features.Auth;
using SuperChat.Api.Security;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Api.Features.Integrations;

public static class IntegrationEndpoints
{
    public static RouteGroupBuilder MapIntegrationEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/integrations")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = ApiSessionAuthenticationHandler.SchemeName });

        group.MapGet(string.Empty, async (
            HttpContext httpContext,
            IIntegrationConnectionService integrationConnectionService,
            IMatrixProvisioningService matrixProvisioningService,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.GetRequiredUserId();
            var matrixIdentity = await matrixProvisioningService.GetIdentityAsync(userId, cancellationToken);
            var connections = await integrationConnectionService.GetConnectionsAsync(userId, cancellationToken);

            return Results.Ok(connections
                .Select(connection => connection.ToIntegrationConnectionResponse(matrixIdentity?.MatrixUserId))
                .ToList());
        });

        group.MapGet("/{provider}", async (
            string provider,
            HttpContext httpContext,
            IIntegrationConnectionService integrationConnectionService,
            IMatrixProvisioningService matrixProvisioningService,
            CancellationToken cancellationToken) =>
        {
            if (!IntegrationProviderExtensions.TryParseRouteSegment(provider, out var parsedProvider))
            {
                return Results.NotFound();
            }

            try
            {
                var userId = httpContext.User.GetRequiredUserId();
                var matrixIdentity = await matrixProvisioningService.GetIdentityAsync(userId, cancellationToken);
                var connection = await integrationConnectionService.GetStatusAsync(userId, parsedProvider, cancellationToken);
                return Results.Ok(connection.ToIntegrationConnectionResponse(matrixIdentity?.MatrixUserId));
            }
            catch (NotSupportedException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status501NotImplemented);
            }
        });

        group.MapPost("/{provider}/connect", async (
            string provider,
            HttpContext httpContext,
            IAuthFlowService authFlowService,
            IIntegrationConnectionService integrationConnectionService,
            IMatrixProvisioningService matrixProvisioningService,
            CancellationToken cancellationToken) =>
        {
            if (!IntegrationProviderExtensions.TryParseRouteSegment(provider, out var parsedProvider))
            {
                return Results.NotFound();
            }

            var user = await authFlowService.FindUserAsync(httpContext.User.GetRequiredEmail(), cancellationToken);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            try
            {
                var connection = await integrationConnectionService.StartAsync(user, parsedProvider, cancellationToken);
                var matrixIdentity = await matrixProvisioningService.GetIdentityAsync(user.Id, cancellationToken);
                return Results.Ok(connection.ToIntegrationConnectionResponse(matrixIdentity?.MatrixUserId));
            }
            catch (NotSupportedException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status501NotImplemented);
            }
        });

        group.MapDelete("/{provider}", async (
            string provider,
            HttpContext httpContext,
            IIntegrationConnectionService integrationConnectionService,
            IMatrixProvisioningService matrixProvisioningService,
            CancellationToken cancellationToken) =>
        {
            if (!IntegrationProviderExtensions.TryParseRouteSegment(provider, out var parsedProvider))
            {
                return Results.NotFound();
            }

            try
            {
                var userId = httpContext.User.GetRequiredUserId();
                await integrationConnectionService.DisconnectAsync(userId, parsedProvider, cancellationToken);
                var connection = await integrationConnectionService.GetStatusAsync(userId, parsedProvider, cancellationToken);
                var matrixIdentity = await matrixProvisioningService.GetIdentityAsync(userId, cancellationToken);
                return Results.Ok(connection.ToIntegrationConnectionResponse(matrixIdentity?.MatrixUserId));
            }
            catch (NotSupportedException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status501NotImplemented);
            }
        });

        return group;
    }
}

public sealed record IntegrationConnectionResponse(
    string Provider,
    string Transport,
    string State,
    string? MatrixUserId,
    Uri? ActionUrl,
    DateTimeOffset? LastSyncedAt,
    bool RequiresAction);
