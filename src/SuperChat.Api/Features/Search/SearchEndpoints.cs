using Microsoft.AspNetCore.Authorization;
using SuperChat.Api.Features.Auth;
using SuperChat.Api.Security;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Features.Search;

namespace SuperChat.Api.Features.Search;

public static class SearchEndpoints
{
    public static RouteGroupBuilder MapSearchEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/search")
            .WithTags("Search")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = ApiSessionAuthenticationHandler.SchemeName });

        group.MapGet(string.Empty, async (
            HttpContext httpContext,
            string q,
            ISearchService searchService,
            CancellationToken cancellationToken) =>
        {
            var results = await searchService.SearchAsync(httpContext.User.GetRequiredUserId(), q, cancellationToken);
            return Results.Ok(results);
        });

        return group;
    }
}
