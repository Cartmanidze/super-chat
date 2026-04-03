using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SuperChat.Contracts.Features.Auth;

namespace SuperChat.Web.Security;

internal sealed class InvalidSessionPageFilter : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context,
        PageHandlerExecutionDelegate next)
    {
        var executed = await next();
        if (executed.Exception is InvalidSessionException)
        {
            LogInvalidSession(context, (InvalidSessionException)executed.Exception);
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            executed.ExceptionHandled = true;
            context.Result = new RedirectToPageResult("/Auth/RequestLink");
        }
    }

    private static void LogInvalidSession(PageHandlerExecutingContext context, InvalidSessionException exception)
    {
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILogger<InvalidSessionPageFilter>>();

        logger.LogWarning(
            "Invalid web session detected. Reason={Reason}. Path={Path}. RequestId={RequestId}. AuthType={AuthType}. IsAuthenticated={IsAuthenticated}. ClaimValueLength={ClaimValueLength}. ClaimValuePreview={ClaimValuePreview}.",
            exception.FailureReason,
            context.HttpContext.Request.Path,
            context.HttpContext.TraceIdentifier,
            context.HttpContext.User.Identity?.AuthenticationType ?? string.Empty,
            context.HttpContext.User.Identity?.IsAuthenticated ?? false,
            exception.UserIdClaimValue?.Length ?? 0,
            BuildClaimValuePreview(exception.UserIdClaimValue));
    }

    private static string BuildClaimValuePreview(string? claimValue)
    {
        if (string.IsNullOrWhiteSpace(claimValue))
        {
            return string.Empty;
        }

        var trimmed = claimValue.Trim();
        return trimmed.Length <= 64
            ? trimmed
            : trimmed[..64];
    }
}
