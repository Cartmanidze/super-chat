using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SuperChat.Api.Features.Health;

public static class HealthEndpoints
{
    public static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/health", async (
            HealthCheckService healthCheckService,
            CancellationToken cancellationToken) =>
        {
            var report = await healthCheckService.CheckHealthAsync(cancellationToken);
            var statusCode = report.Status == HealthStatus.Healthy
                ? StatusCodes.Status200OK
                : StatusCodes.Status503ServiceUnavailable;

            return Results.Json(new
            {
                status = report.Status == HealthStatus.Healthy ? "ok" : "error"
            }, statusCode: statusCode);
        })
        .WithTags("Health");

        return api;
    }
}
