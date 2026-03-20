using Microsoft.AspNetCore.Authorization;
using SuperChat.Api.Features.Auth;
using SuperChat.Api.Security;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Features.Feedback;

namespace SuperChat.Api.Features.Feedback;

public static class FeedbackEndpoints
{
    public static RouteGroupBuilder MapFeedbackEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/feedback")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = ApiSessionAuthenticationHandler.SchemeName });

        group.MapPost(string.Empty, async (
            HttpContext httpContext,
            FeedbackRequest request,
            IFeedbackService feedbackService,
            CancellationToken cancellationToken) =>
        {
            await feedbackService.RecordAsync(
                httpContext.User.GetRequiredUserId(),
                request.Area,
                request.Useful,
                request.Note,
                cancellationToken);

            return Results.Accepted(value: new FeedbackResponse("recorded"));
        });

        return group;
    }
}

public sealed record FeedbackRequest(string Area, bool Useful, string? Note);

public sealed record FeedbackResponse(string Status);
