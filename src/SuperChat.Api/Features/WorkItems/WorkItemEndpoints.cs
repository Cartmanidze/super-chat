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

        MapTypedActionEndpoints(group.MapGroup("/requests"), WorkItemType.Request);
        MapTypedActionEndpoints(group.MapGroup("/events"), WorkItemType.Event);
        MapTypedActionEndpoints(group.MapGroup("/action-items"), WorkItemType.ActionItem);

        return group;
    }

    private static void MapTypedActionEndpoints(RouteGroupBuilder group, WorkItemType type)
    {
        group.MapPost("/complete", async (
            HttpContext httpContext,
            WorkItemActionRequest request,
            IWorkItemActionService workItemActionService,
            CancellationToken cancellationToken) =>
        {
            return await ExecuteActionAsync(
                request,
                actionKey => workItemActionService.CompleteAsync(
                    httpContext.User.GetRequiredUserId(),
                    type,
                    actionKey,
                    cancellationToken));
        });

        group.MapPost("/dismiss", async (
            HttpContext httpContext,
            WorkItemActionRequest request,
            IWorkItemActionService workItemActionService,
            CancellationToken cancellationToken) =>
        {
            return await ExecuteActionAsync(
                request,
                actionKey => workItemActionService.DismissAsync(
                    httpContext.User.GetRequiredUserId(),
                    type,
                    actionKey,
                    cancellationToken));
        });
    }

    private static async Task<IResult> ExecuteActionAsync(
        WorkItemActionRequest request,
        Func<string, Task<bool>> resolveAsync)
    {
        if (string.IsNullOrWhiteSpace(request.ActionKey))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["actionKey"] = ["Action key is required."]
            });
        }

        var resolved = await resolveAsync(request.ActionKey);
        return resolved ? Results.NoContent() : Results.NotFound();
    }
}

public sealed record WorkItemActionRequest(string ActionKey);
