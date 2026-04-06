using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace SuperChat.Infrastructure.Diagnostics;

public static class SuperChatStructuredLoggingExtensions
{
    public const string CorrelationIdHeaderName = "X-Correlation-ID";

    public static WebApplicationBuilder AddSuperChatStructuredLogging(
        this WebApplicationBuilder builder,
        string serviceName)
    {
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog((services, loggerConfiguration) =>
        {
            ConfigureLogger(
                loggerConfiguration,
                builder.Configuration,
                builder.Environment.EnvironmentName,
                serviceName,
                services);
        });

        return builder;
    }

    public static HostApplicationBuilder AddSuperChatStructuredLogging(
        this HostApplicationBuilder builder,
        string serviceName)
    {
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog((services, loggerConfiguration) =>
        {
            ConfigureLogger(
                loggerConfiguration,
                builder.Configuration,
                builder.Environment.EnvironmentName,
                serviceName,
                services);
        });

        return builder;
    }

    public static IApplicationBuilder UseSuperChatRequestLogging(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
            options.GetLevel = ResolveRequestLevel;
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("CorrelationId", httpContext.TraceIdentifier);
                diagnosticContext.Set("RequestMethod", httpContext.Request.Method);
                diagnosticContext.Set("RequestPath", httpContext.Request.Path.Value ?? string.Empty);
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? string.Empty);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);

                if (Activity.Current is { } activity)
                {
                    diagnosticContext.Set("TraceId", activity.TraceId.ToString());
                    diagnosticContext.Set("SpanId", activity.SpanId.ToString());
                }
            };
        });

        return app;
    }

    private static LogEventLevel ResolveRequestLevel(HttpContext httpContext, double _, Exception? exception)
    {
        if (exception is not null || httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            return LogEventLevel.Error;
        }

        if (string.Equals(httpContext.Request.Path, "/metrics", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(httpContext.Request.Path, "/health", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(httpContext.Request.Path, "/api/v1/health", StringComparison.OrdinalIgnoreCase))
        {
            return LogEventLevel.Debug;
        }

        return httpContext.Response.StatusCode >= StatusCodes.Status400BadRequest
            ? LogEventLevel.Warning
            : LogEventLevel.Information;
    }

    private static void ConfigureLogger(
        LoggerConfiguration loggerConfiguration,
        IConfiguration configuration,
        string environmentName,
        string serviceName,
        IServiceProvider services)
    {
        ApplyMinimumLevels(loggerConfiguration, configuration);

        loggerConfiguration
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "super-chat")
            .Enrich.WithProperty("Environment", environmentName)
            .Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console(new JsonFormatter(renderMessage: true));
    }

    private static void ApplyMinimumLevels(
        LoggerConfiguration loggerConfiguration,
        IConfiguration configuration)
    {
        var levelsSection = configuration.GetSection("Logging:LogLevel");
        var defaultLevel = ParseLevel(levelsSection["Default"], LogEventLevel.Information);
        loggerConfiguration.MinimumLevel.Is(defaultLevel);

        foreach (var child in levelsSection.GetChildren())
        {
            if (string.Equals(child.Key, "Default", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(child.Value))
            {
                continue;
            }

            loggerConfiguration.MinimumLevel.Override(child.Key, ParseLevel(child.Value, defaultLevel));
        }
    }

    private static LogEventLevel ParseLevel(string? value, LogEventLevel fallback)
    {
        return Enum.TryParse<LogEventLevel>(value, ignoreCase: true, out var parsed)
            ? parsed
            : fallback;
    }
}
