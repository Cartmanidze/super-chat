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
            var statusCode = report.Status == HealthStatus.Unhealthy
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status200OK;

            var status = report.Status == HealthStatus.Unhealthy ? "error" : "ok";

            return Results.Json(new
            {
                status
            }, statusCode: statusCode);
        })
        .WithTags("Health");

        return api;
    }
}
