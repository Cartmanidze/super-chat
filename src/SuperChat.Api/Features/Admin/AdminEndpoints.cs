using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using SuperChat.Api.Features.Auth;
using SuperChat.Api.Security;
using SuperChat.Contracts.Features.Admin;
using SuperChat.Contracts.Features.Auth;

namespace SuperChat.Api.Features.Admin;

public static class AdminEndpoints
{
    private const string AdminPasswordHeader = "X-SuperChat-Admin-Password";

    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/admin")
            .WithTags("Admin")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = ApiSessionAuthenticationHandler.SchemeName });

        group.MapPost("/unlock", (
            HttpContext httpContext,
            AdminUnlockRequest request,
            IOptions<PilotOptions> pilotOptions,
            IAdminPasswordService adminPasswordService) =>
        {
            return TryAuthorizeAdmin(httpContext.User.GetRequiredEmail(), request.Password, pilotOptions.Value, adminPasswordService)
                ? Results.Ok(new AdminUnlockResponse(true))
                : Results.Problem(title: "Admin access denied", detail: "Invalid admin credentials.", statusCode: StatusCodes.Status403Forbidden);
        });

        group.MapGet("/invites", async (
            HttpContext httpContext,
            IPilotInviteAdminService pilotInviteAdminService,
            IOptions<PilotOptions> pilotOptions,
            IAdminPasswordService adminPasswordService,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveAdminPassword(httpContext, out var password) ||
                !TryAuthorizeAdmin(httpContext.User.GetRequiredEmail(), password, pilotOptions.Value, adminPasswordService))
            {
                return Results.Problem(title: "Admin access denied", detail: "Invalid admin credentials.", statusCode: StatusCodes.Status403Forbidden);
            }

            var invites = await pilotInviteAdminService.GetInvitesAsync(cancellationToken);
            return Results.Ok(invites);
        });

        group.MapPost("/invites", async (
            HttpContext httpContext,
            AdminInviteRequest request,
            IPilotInviteAdminService pilotInviteAdminService,
            IOptions<PilotOptions> pilotOptions,
            IAdminPasswordService adminPasswordService,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveAdminPassword(httpContext, out var password) ||
                !TryAuthorizeAdmin(httpContext.User.GetRequiredEmail(), password, pilotOptions.Value, adminPasswordService))
            {
                return Results.Problem(title: "Admin access denied", detail: "Invalid admin credentials.", statusCode: StatusCodes.Status403Forbidden);
            }

            var result = await pilotInviteAdminService.AddInviteAsync(request.Email, httpContext.User.GetRequiredEmail(), cancellationToken);
            return Results.Ok(result);
        });

        return group;
    }

    private static bool TryResolveAdminPassword(HttpContext httpContext, out string password)
    {
        password = httpContext.Request.Headers[AdminPasswordHeader].ToString().Trim();
        return !string.IsNullOrWhiteSpace(password);
    }

    private static bool TryAuthorizeAdmin(
        string email,
        string password,
        PilotOptions options,
        IAdminPasswordService adminPasswordService)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var isConfiguredAdmin = options.AdminEmails.Any(candidate =>
            !string.IsNullOrWhiteSpace(candidate) &&
            string.Equals(candidate.Trim(), email, StringComparison.OrdinalIgnoreCase));

        return isConfiguredAdmin && adminPasswordService.Verify(password);
    }
}

public sealed record AdminUnlockRequest(string Password);

public sealed record AdminUnlockResponse(bool Unlocked);

public sealed record AdminInviteRequest(string Email);
