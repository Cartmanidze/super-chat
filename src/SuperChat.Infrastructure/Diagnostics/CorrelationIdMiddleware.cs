using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace SuperChat.Infrastructure.Diagnostics;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.TraceIdentifier = correlationId;
        context.Response.Headers[SuperChatStructuredLoggingExtensions.CorrelationIdHeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestMethod", context.Request.Method))
        using (LogContext.PushProperty("RequestPath", context.Request.Path.Value ?? string.Empty))
        using (PushActivityProperties())
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var headerValue = context.Request.Headers[SuperChatStructuredLoggingExtensions.CorrelationIdHeaderName]
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(headerValue)
            ? context.TraceIdentifier
            : headerValue.Trim();
    }

    private static IDisposable? PushActivityProperties()
    {
        var activity = Activity.Current;
        if (activity is null)
        {
            return null;
        }

        return new CompositeDisposable(
            LogContext.PushProperty("TraceId", activity.TraceId.ToString()),
            LogContext.PushProperty("SpanId", activity.SpanId.ToString()));
    }

    private sealed class CompositeDisposable(
        IDisposable first,
        IDisposable second) : IDisposable
    {
        private readonly IDisposable _first = first;
        private readonly IDisposable _second = second;

        public void Dispose()
        {
            _second.Dispose();
            _first.Dispose();
        }
    }
}
