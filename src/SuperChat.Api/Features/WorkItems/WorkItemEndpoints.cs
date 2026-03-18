using Microsoft.AspNetCore.Authorization;
using SuperChat.Contracts.ViewModels;
using SuperChat.Api.Features.Auth;
using SuperChat.Api.Security;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Api.Features.WorkItems;

public static class WorkItemEndpoints
{
    public static RouteGroupBuilder MapWorkItemEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/work-items")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = ApiSessionAuthenticationHandler.SchemeName });

        group.MapGet(string.Empty, async (
            HttpContext httpContext,
            WorkItemType? type,
            IWorkItemCatalogService workItemCatalogService,
            CancellationToken cancellationToken) =>
        {
            var cards = await workItemCatalogService.ListAsync(
                httpContext.User.GetRequiredUserId(),
                type,
                cancellationToken);

            return Results.Ok(cards);
        });

        group.MapGet("/search", async (
            HttpContext httpContext,
            string q,
            WorkItemType? type,
            IWorkItemCatalogService workItemCatalogService,
            CancellationToken cancellationToken) =>
        {
            var cards = await workItemCatalogService.SearchAsync(
                httpContext.User.GetRequiredUserId(),
                q,
                type,
                cancellationToken);

            return Results.Ok(cards);
        });

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

        MapRequestActionEndpoints(group.MapGroup("/requests"));
        MapEventActionEndpoints(group.MapGroup("/events"));
        MapActionItemActionEndpoints(group.MapGroup("/action-items"));

        return group;
    }

    private static void MapRequestActionEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/complete", async (
            HttpContext httpContext,
            Guid id,
            IRequestWorkItemCommandService requestWorkItemCommandService,
            CancellationToken cancellationToken) =>
        {
            var resolved = await requestWorkItemCommandService.CompleteAsync(
                httpContext.User.GetRequiredUserId(),
                id,
                cancellationToken);

            return resolved ? Results.NoContent() : Results.NotFound();
        });

        group.MapPost("/{id:guid}/dismiss", async (
            HttpContext httpContext,
            Guid id,
            IRequestWorkItemCommandService requestWorkItemCommandService,
            CancellationToken cancellationToken) =>
        {
            var resolved = await requestWorkItemCommandService.DismissAsync(
                httpContext.User.GetRequiredUserId(),
                id,
                cancellationToken);

            return resolved ? Results.NoContent() : Results.NotFound();
        });
    }

    private static void MapEventActionEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/complete", async (
            HttpContext httpContext,
            Guid id,
            IEventWorkItemCommandService eventWorkItemCommandService,
            CancellationToken cancellationToken) =>
        {
            var resolved = await eventWorkItemCommandService.CompleteAsync(
                httpContext.User.GetRequiredUserId(),
                id,
                cancellationToken);

            return resolved ? Results.NoContent() : Results.NotFound();
        });

        group.MapPost("/{id:guid}/dismiss", async (
            HttpContext httpContext,
            Guid id,
            IEventWorkItemCommandService eventWorkItemCommandService,
            CancellationToken cancellationToken) =>
        {
            var resolved = await eventWorkItemCommandService.DismissAsync(
                httpContext.User.GetRequiredUserId(),
                id,
                cancellationToken);

            return resolved ? Results.NoContent() : Results.NotFound();
        });
    }

    private static void MapActionItemActionEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/complete", async (
            HttpContext httpContext,
            Guid id,
            IActionItemWorkItemCommandService actionItemWorkItemCommandService,
            CancellationToken cancellationToken) =>
        {
            var resolved = await actionItemWorkItemCommandService.CompleteAsync(
                httpContext.User.GetRequiredUserId(),
                id,
                cancellationToken);

            return resolved ? Results.NoContent() : Results.NotFound();
        });

        group.MapPost("/{id:guid}/dismiss", async (
            HttpContext httpContext,
            Guid id,
            IActionItemWorkItemCommandService actionItemWorkItemCommandService,
            CancellationToken cancellationToken) =>
        {
            var resolved = await actionItemWorkItemCommandService.DismissAsync(
                httpContext.User.GetRequiredUserId(),
                id,
                cancellationToken);

            return resolved ? Results.NoContent() : Results.NotFound();
        });
    }
}
