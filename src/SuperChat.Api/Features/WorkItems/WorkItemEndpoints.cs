using Microsoft.AspNetCore.Authorization;
using SuperChat.Api.Features.Auth;
using SuperChat.Api.Security;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Contracts.Features.WorkItems;

namespace SuperChat.Api.Features.WorkItems;

public static class WorkItemEndpoints
{
    public static RouteGroupBuilder MapWorkItemEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/work-items")
            .WithTags("Meetings")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = ApiSessionAuthenticationHandler.SchemeName });

        group.MapGet("/meetings", async (
            HttpContext httpContext,
            IDigestService digestService,
            CancellationToken cancellationToken) =>
        {
            var cards = await digestService.GetMeetingsAsync(httpContext.User.GetRequiredUserId(), cancellationToken);
            return Results.Ok(cards);
        });

        MapMeetingActionEndpoints(group.MapGroup("/meetings"));

        return group;
    }

    private static void MapMeetingActionEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/complete", async (
            HttpContext httpContext,
            Guid id,
            IMeetingWorkItemCommandService meetingWorkItemCommandService,
            CancellationToken cancellationToken) =>
        {
            var resolved = await meetingWorkItemCommandService.CompleteAsync(
                httpContext.User.GetRequiredUserId(),
                id,
                cancellationToken);

            return resolved ? Results.NoContent() : Results.NotFound();
        });

        group.MapPost("/{id:guid}/dismiss", async (
            HttpContext httpContext,
            Guid id,
            IMeetingWorkItemCommandService meetingWorkItemCommandService,
            CancellationToken cancellationToken) =>
        {
            var resolved = await meetingWorkItemCommandService.DismissAsync(
                httpContext.User.GetRequiredUserId(),
                id,
                cancellationToken);

            return resolved ? Results.NoContent() : Results.NotFound();
        });
    }
}
