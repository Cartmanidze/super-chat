using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Prometheus;
using SuperChat.Infrastructure.Diagnostics;
using SuperChat.Worker;
using SuperChat.Worker.Features.Integrations.Max.Internal;
using SuperChat.Worker.Features.Integrations.Telegram.Internal;

var builder = WebApplication.CreateBuilder(args);
builder.AddSuperChatStructuredLogging("superchat-worker");

WorkerServiceConfiguration.AddWorkerServices(builder.Services, builder.Configuration);

var app = builder.Build();

app.UseSuperChatRequestLogging();
app.UseHttpMetrics();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = WriteHealthResponseAsync
});
app.MapMetrics();
app.MapGet("/", () => Results.Json(new { status = "worker" })).ExcludeFromDescription();
var internalApi = app.MapGroup("/api/v1");
internalApi.MapTelegramInternalEndpoints();
internalApi.MapMaxInternalEndpoints();

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
