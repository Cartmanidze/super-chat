using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Prometheus;
using SuperChat.Infrastructure.Composition;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSuperChatBootstrap(
    builder.Configuration,
    enableMatrixSyncWorker: false,
    enablePipelineScheduling: false,
    enablePipelineConsumers: true);

var app = builder.Build();

app.UseHttpMetrics();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = WriteHealthResponseAsync
});
app.MapMetrics();
app.MapGet("/", () => Results.Json(new { status = "worker" })).ExcludeFromDescription();

await app.RunAsync();

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json; charset=utf-8";
    return context.Response.WriteAsJsonAsync(new
    {
        status = report.Status == HealthStatus.Healthy ? "ok" : "error"
    });
}

namespace SuperChat.Worker
{
    public partial class Program;
}
