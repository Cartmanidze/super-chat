using Microsoft.AspNetCore.Authorization;
using SuperChat.Api.Features.Auth;
using SuperChat.Api.Security;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Api.Features.Dashboard;

public static class DashboardEndpoints
{
    public static RouteGroupBuilder MapDashboardEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/dashboard")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = ApiSessionAuthenticationHandler.SchemeName });

        group.MapGet("/today", async (
            HttpContext httpContext,
            IDigestService digestService,
            CancellationToken cancellationToken) =>
        {
            var cards = await digestService.GetTodayAsync(httpContext.User.GetRequiredUserId(), cancellationToken);
            return Results.Ok(cards);
        });

        group.MapGet("/waiting", async (
            HttpContext httpContext,
            IDigestService digestService,
            CancellationToken cancellationToken) =>
        {
            var cards = await digestService.GetWaitingAsync(httpContext.User.GetRequiredUserId(), cancellationToken);
            return Results.Ok(cards);
        });

        group.MapGet("/meetings", async (
            HttpContext httpContext,
            IDigestService digestService,
            CancellationToken cancellationToken) =>
        {
            var cards = await digestService.GetMeetingsAsync(httpContext.User.GetRequiredUserId(), cancellationToken);
            return Results.Ok(cards);
        });

        return group;
    }
}
