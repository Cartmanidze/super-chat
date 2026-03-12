using Microsoft.AspNetCore.Authorization;
using SuperChat.Api.Features.Auth;
using SuperChat.Api.Security;
using SuperChat.Contracts.ViewModels;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Api.Features.Chat;

public static class ChatEndpoints
{
    public static RouteGroupBuilder MapChatEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/chat")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = ApiSessionAuthenticationHandler.SchemeName });

        group.MapPost("/ask", async (
            HttpContext httpContext,
            ChatPromptRequest request,
            IChatExperienceService chatExperienceService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var answer = await chatExperienceService.AskAsync(httpContext.User.GetRequiredUserId(), request, cancellationToken);
                return Results.Ok(answer);
            }
            catch (ArgumentException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["question"] = [ex.Message]
                });
            }
        });

        return group;
    }
}
