using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Logging;
using SuperChat.Contracts.Features.Auth;

namespace SuperChat.Api.Security;

internal sealed class InvalidSessionExceptionHandler(
    ILogger<InvalidSessionExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not InvalidSessionException invalidSessionException)
            return false;

        logger.LogWarning(
            "Invalid API session detected. Reason={Reason}. Path={Path}. RequestId={RequestId}. AuthType={AuthType}. IsAuthenticated={IsAuthenticated}. ClaimValueLength={ClaimValueLength}. ClaimValuePreview={ClaimValuePreview}.",
            invalidSessionException.FailureReason,
            httpContext.Request.Path,
            httpContext.TraceIdentifier,
            httpContext.User.Identity?.AuthenticationType ?? string.Empty,
            httpContext.User.Identity?.IsAuthenticated ?? false,
            invalidSessionException.UserIdClaimValue?.Length ?? 0,
            BuildClaimValuePreview(invalidSessionException.UserIdClaimValue));

        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await httpContext.Response.WriteAsJsonAsync(
            new { error = "Unauthorized", message = "User session is missing or corrupted." },
            cancellationToken);
        return true;
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
