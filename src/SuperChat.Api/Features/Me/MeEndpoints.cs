using Microsoft.AspNetCore.Authorization;
using SuperChat.Api.Features.Auth;
using SuperChat.Api.Security;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.State;

namespace SuperChat.Api.Features.Me;

public static class MeEndpoints
{
    public static RouteGroupBuilder MapMeEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/me", [Authorize(AuthenticationSchemes = ApiSessionAuthenticationHandler.SchemeName)] async (
            HttpContext httpContext,
            ITelegramConnectionService telegramConnectionService,
            SuperChatStore store,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.GetRequiredUserId();
            var email = httpContext.User.GetRequiredEmail();
            var matrixIdentity = store.GetMatrixIdentity(userId);
            var connection = await telegramConnectionService.GetStatusAsync(userId, cancellationToken);

            return Results.Ok(new MeResponse(
                Id: userId,
                Email: email,
                MatrixUserId: matrixIdentity?.MatrixUserId,
                TelegramState: connection.State.ToString(),
                LastSyncedAt: connection.LastSyncedAt,
                RequiresTelegramAction: connection.State is not Domain.Model.TelegramConnectionState.Connected));
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
